using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Oxide.Core;
using UnityEngine;

namespace Carbon.Hooks;

#if RUST_STAGING
internal static class StagingRustIlCompat
{
	private const string HarmonyId = "carbon.staging.rust.il.compat";

	private static readonly Harmony Harmony = new(HarmonyId);
	private static bool _installed;

	public static void Install()
	{
		if (_installed)
		{
			return;
		}

		int patches = 0;

		patches += PatchTranspiler(
			AccessTools.Method(typeof(BaseFishingRod), "Server_RequestCast", new[] { typeof(BaseEntity.RPCMessage) }),
			nameof(ServerRequestCastTranspiler),
			"BaseFishingRod.Server_RequestCast");

		patches += PatchTranspiler(
			AccessTools.Method(typeof(BaseFishingRod), "CatchProcessBudgeted", Type.EmptyTypes),
			nameof(CatchProcessBudgetedTranspiler),
			"BaseFishingRod.CatchProcessBudgeted");

		patches += PatchTranspiler(
			AccessTools.Method(typeof(ResourceDispenser), "GiveResourceFromItem", new[] { typeof(BasePlayer), typeof(ItemAmount), typeof(float), typeof(float), typeof(AttackEntity) }),
			nameof(GiveResourceFromItemTranspiler),
			"ResourceDispenser.GiveResourceFromItem");

		patches += PatchTranspiler(
			AccessTools.Method(typeof(ResourceDispenser), "AssignFinishBonus", new[] { typeof(BasePlayer), typeof(float), typeof(AttackEntity) }),
			nameof(AssignFinishBonusTranspiler),
			"ResourceDispenser.AssignFinishBonus");

		patches += PatchTranspiler(
			AccessTools.Method(typeof(ItemCrafter), "CraftItem", new[] { typeof(ItemBlueprint), typeof(BasePlayer), typeof(ProtoBuf.Item.InstanceData), typeof(int), typeof(int), typeof(Item), typeof(bool), typeof(int) }),
			nameof(CraftItemTranspiler),
			"ItemCrafter.CraftItem");

		patches += PatchPostfix(
			AccessTools.Method(typeof(NPCPlayer), "CreateCorpse", new[] { typeof(BasePlayer.PlayerFlags), typeof(Vector3), typeof(Quaternion), typeof(List<TriggerBase>), typeof(bool) }),
			nameof(CreateCorpsePostfix),
			"NPCPlayer.CreateCorpse");

		patches += PatchPrefix(
			AccessTools.Method(typeof(BasePlayer), "OnReceivedVoice", new[] { typeof(ReadOnlySpan<byte>) }),
			nameof(OnReceivedVoicePrefix),
			"BasePlayer.OnReceivedVoice(ReadOnlySpan<byte>)");

		_installed = patches > 0;

		if (_installed && !Community.Runtime.Config.Logging.ReducedLogging)
		{
			Logger.Log($" Installed {patches} staging Rust IL compatibility patches.");
		}
	}

	public static void Uninstall()
	{
		if (!_installed)
		{
			return;
		}

		try
		{
			Harmony.UnpatchAll(HarmonyId);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed uninstalling staging Rust IL compatibility patches.", ex);
		}
		finally
		{
			_installed = false;
		}
	}

	private static int PatchTranspiler(MethodInfo target, string transpilerName, string description)
	{
		MethodInfo transpiler = AccessTools.Method(typeof(StagingRustIlCompat), transpilerName);

		if (target == null || transpiler == null)
		{
			Logger.Warn($" Unable to install staging compatibility patch for {description}: target or transpiler was not found.");
			return 0;
		}

		try
		{
			Harmony.Patch(target, transpiler: new HarmonyMethod(transpiler));
			return 1;
		}
		catch (Exception ex)
		{
			Logger.Error($"Failed installing staging compatibility patch for {description}.", ex);
			return 0;
		}
	}

	private static int PatchPrefix(MethodInfo target, string prefixName, string description)
	{
		MethodInfo prefix = AccessTools.Method(typeof(StagingRustIlCompat), prefixName);

		if (target == null || prefix == null)
		{
			Logger.Warn($" Unable to install staging compatibility prefix for {description}: target or prefix was not found.");
			return 0;
		}

		try
		{
			Harmony.Patch(target, prefix: new HarmonyMethod(prefix));
			return 1;
		}
		catch (Exception ex)
		{
			Logger.Error($"Failed installing staging compatibility prefix for {description}.", ex);
			return 0;
		}
	}

	private static int PatchPostfix(MethodInfo target, string postfixName, string description)
	{
		MethodInfo postfix = AccessTools.Method(typeof(StagingRustIlCompat), postfixName);

		if (target == null || postfix == null)
		{
			Logger.Warn($" Unable to install staging compatibility postfix for {description}: target or postfix was not found.");
			return 0;
		}

		try
		{
			Harmony.Patch(target, postfix: new HarmonyMethod(postfix));
			return 1;
		}
		catch (Exception ex)
		{
			Logger.Error($"Failed installing staging compatibility postfix for {description}.", ex);
			return 0;
		}
	}

	private static IEnumerable<CodeInstruction> ServerRequestCastTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
	{
		List<CodeInstruction> code = new(instructions);

		try
		{
			int evaluateCall = code.FindIndex(instruction => CallsNamed(instruction, "EvaluateFishingPosition"));
			int branchIndex = evaluateCall >= 0 ? evaluateCall + 1 : -1;

			if (branchIndex < 0 || branchIndex >= code.Count || !IsBranch(code[branchIndex], out Label continueCastLabel))
			{
				throw new InvalidOperationException("Unable to find EvaluateFishingPosition success branch.");
			}

			int insertIndex = FindLabelIndex(code, continueCastLabel);
			Label continueOriginal = generator.DefineLabel();

			List<CodeInstruction> insert = new()
			{
				LoadLocal(1),
				LoadArg(0),
				LoadLocal(2),
				LoadLocal(0),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StagingRustIlCompat), nameof(ShouldCancelFishingCast))),
				new CodeInstruction(OpCodes.Brfalse_S, continueOriginal),
				new CodeInstruction(OpCodes.Ret)
			};

			MoveLabels(code[insertIndex], insert[0]);
			code[insertIndex].labels.Add(continueOriginal);
			code.InsertRange(insertIndex, insert);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed applying staging compatibility transpiler for BaseFishingRod.Server_RequestCast.", ex);
		}

		return code;
	}

	private static IEnumerable<CodeInstruction> CatchProcessBudgetedTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
	{
		List<CodeInstruction> code = new(instructions);

		try
		{
			int ownershipCall = code.FindIndex(instruction => CallsNamed(instruction, typeof(Item), "SetItemOwnership"));
			int popIndex = ownershipCall >= 0 && ownershipCall + 1 < code.Count && code[ownershipCall + 1].opcode == OpCodes.Pop
				? ownershipCall + 1
				: -1;

			if (popIndex < 0)
			{
				throw new InvalidOperationException("Unable to find SetItemOwnership result pop.");
			}

			object leaveTarget = FindLastLeaveTarget(code);

			if (leaveTarget == null)
			{
				throw new InvalidOperationException("Unable to find method-end leave target.");
			}

			Label continueLabel = generator.DefineLabel();
			CodeInstruction onFishItem = LoadLocal(16);
			onFishItem.labels.Add(continueLabel);

			code.InsertRange(popIndex + 1, new[]
			{
				LoadLocal(2),
				LoadArg(0),
				LoadLocal(16),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StagingRustIlCompat), nameof(ShouldCancelFishCatch))),
				new CodeInstruction(OpCodes.Brfalse_S, continueLabel),
				new CodeInstruction(OpCodes.Leave, leaveTarget),
				onFishItem,
				LoadArg(0),
				LoadLocal(2),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StagingRustIlCompat), nameof(OnFishCatchCompat))),
				StoreLocal(16)
			});
		}
		catch (Exception ex)
		{
			Logger.Error("Failed applying staging compatibility transpiler for BaseFishingRod.CatchProcessBudgeted.", ex);
		}

		return code;
	}

	private static IEnumerable<CodeInstruction> GiveResourceFromItemTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
	{
		List<CodeInstruction> code = new(instructions);

		try
		{
			int createCall = code.FindIndex(instruction => CallsNamed(instruction, typeof(ItemManager), "CreateByItemID"));
			int branchIndex = FindNextBranch(code, createCall, OpCodes.Brtrue, OpCodes.Brtrue_S);

			if (branchIndex < 0 || !IsBranch(code[branchIndex], out Label itemCreatedLabel))
			{
				throw new InvalidOperationException("Unable to find created-item branch.");
			}

			int insertIndex = FindLabelIndex(code, itemCreatedLabel);
			Label continueOriginal = generator.DefineLabel();

			List<CodeInstruction> insert = new()
			{
				LoadArg(0),
				LoadArg(1),
				LoadLocal(7),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StagingRustIlCompat), nameof(ShouldSkipDispenserGather))),
				new CodeInstruction(OpCodes.Brfalse_S, continueOriginal),
				new CodeInstruction(OpCodes.Ret)
			};

			MoveLabels(code[insertIndex], insert[0]);
			code[insertIndex].labels.Add(continueOriginal);
			code.InsertRange(insertIndex, insert);

			int analyticsCall = code.FindIndex(insertIndex, instruction => CallsNamed(instruction, "OnGatherItem"));

			if (analyticsCall < 0)
			{
				throw new InvalidOperationException("Unable to find gather analytics call.");
			}

			code.InsertRange(analyticsCall + 1, new[]
			{
				LoadArg(0),
				LoadArg(1),
				LoadLocal(7),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StagingRustIlCompat), nameof(OnDispenserGatheredCompat)))
			});
		}
		catch (Exception ex)
		{
			Logger.Error("Failed applying staging compatibility transpiler for ResourceDispenser.GiveResourceFromItem.", ex);
		}

		return code;
	}

	private static IEnumerable<CodeInstruction> AssignFinishBonusTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
	{
		List<CodeInstruction> code = new(instructions);

		try
		{
			int createCall = code.FindIndex(instruction => CallsNamed(instruction, typeof(ItemManager), "Create"));
			int branchIndex = FindNextBranch(code, createCall, OpCodes.Brfalse, OpCodes.Brfalse_S);

			if (branchIndex < 0 || !IsBranch(code[branchIndex], out Label noItemLabel))
			{
				throw new InvalidOperationException("Unable to find null-item branch.");
			}

			int insertIndex = branchIndex + 1;

			List<CodeInstruction> insert = new()
			{
				LoadArg(0),
				LoadArg(1),
				LoadLocal(4),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StagingRustIlCompat), nameof(OnDispenserBonusCompat))),
				StoreLocal(4),
				LoadLocal(4),
				new CodeInstruction(OpCodes.Brfalse_S, noItemLabel)
			};

			MoveLabels(code[insertIndex], insert[0]);
			code.InsertRange(insertIndex, insert);

			int analyticsCall = code.FindIndex(insertIndex, instruction => CallsNamed(instruction, "OnGatherItem"));

			if (analyticsCall < 0)
			{
				throw new InvalidOperationException("Unable to find bonus analytics call.");
			}

			code.InsertRange(analyticsCall + 1, new[]
			{
				LoadArg(0),
				LoadArg(1),
				LoadLocal(4),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StagingRustIlCompat), nameof(OnDispenserBonusReceivedCompat)))
			});
		}
		catch (Exception ex)
		{
			Logger.Error("Failed applying staging compatibility transpiler for ResourceDispenser.AssignFinishBonus.", ex);
		}

		return code;
	}

	private static IEnumerable<CodeInstruction> CraftItemTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
	{
		List<CodeInstruction> code = new(instructions);

		try
		{
			int addLastCall = code.FindIndex(instruction => CallsNamed(instruction, "AddLast"));
			int insertIndex = addLastCall >= 3 ? addLastCall - 3 : -1;

			if (insertIndex < 0)
			{
				throw new InvalidOperationException("Unable to find crafting queue insertion.");
			}

			LocalBuilder resultLocal = generator.DeclareLocal(typeof(int));
			Label continueOriginal = generator.DefineLabel();

			List<CodeInstruction> insert = new()
			{
				LoadLocal(0),
				LoadArg(2),
				LoadArg(6),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StagingRustIlCompat), nameof(OnItemCraftCompat))),
				new CodeInstruction(OpCodes.Stloc, resultLocal),
				new CodeInstruction(OpCodes.Ldloc, resultLocal),
				new CodeInstruction(OpCodes.Ldc_I4_M1),
				new CodeInstruction(OpCodes.Beq_S, continueOriginal),
				new CodeInstruction(OpCodes.Ldloc, resultLocal),
				new CodeInstruction(OpCodes.Ldc_I4_1),
				new CodeInstruction(OpCodes.Ceq),
				new CodeInstruction(OpCodes.Ret)
			};

			MoveLabels(code[insertIndex], insert[0]);
			code[insertIndex].labels.Add(continueOriginal);
			code.InsertRange(insertIndex, insert);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed applying staging compatibility transpiler for ItemCrafter.CraftItem.", ex);
		}

		return code;
	}

	private static void CreateCorpsePostfix(NPCPlayer __instance, BaseCorpse __result)
	{
		if (__instance == null || __result is not LootableCorpse corpse)
		{
			return;
		}

		try
		{
			Interface.CallHook("OnCorpsePopulate", __instance, corpse);
		}
		catch (Exception ex)
		{
			Logger.Error("Staging OnCorpsePopulate compatibility hook failed.", ex);
		}
	}

	private static bool OnReceivedVoicePrefix(BasePlayer __instance, ReadOnlySpan<byte> data)
	{
		if (__instance == null)
		{
			return true;
		}

		try
		{
			byte[] payload = data.Length == 0 ? Array.Empty<byte>() : data.ToArray();
			object result = Interface.CallHook("OnPlayerVoice", __instance, payload);
			return result == null;
		}
		catch (Exception ex)
		{
			Logger.Error("Staging OnPlayerVoice compatibility hook failed.", ex);
			return true;
		}
	}

	private static bool ShouldCancelFishingCast(BasePlayer player, BaseFishingRod rod, Item lure, Vector3 position)
	{
		try
		{
			return Interface.CallHook("CanCastFishingRod", player, rod, lure, position) is bool allowed && !allowed;
		}
		catch (Exception ex)
		{
			Logger.Error("Staging CanCastFishingRod compatibility hook failed.", ex);
			return false;
		}
	}

	private static bool ShouldCancelFishCatch(BasePlayer player, BaseFishingRod rod, Item item)
	{
		try
		{
			return Interface.CallHook("CanCatchFish", player, rod, item) is bool allowed && !allowed;
		}
		catch (Exception ex)
		{
			Logger.Error("Staging CanCatchFish compatibility hook failed.", ex);
			return false;
		}
	}

	private static Item OnFishCatchCompat(Item item, BaseFishingRod rod, BasePlayer player)
	{
		try
		{
			return Interface.CallHook("OnFishCatch", item, rod, player) is Item replacement ? replacement : item;
		}
		catch (Exception ex)
		{
			Logger.Error("Staging OnFishCatch compatibility hook failed.", ex);
			return item;
		}
	}

	private static bool ShouldSkipDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
	{
		try
		{
			return Interface.CallHook("OnDispenserGather", dispenser, player, item) != null;
		}
		catch (Exception ex)
		{
			Logger.Error("Staging OnDispenserGather compatibility hook failed.", ex);
			return false;
		}
	}

	private static void OnDispenserGatheredCompat(ResourceDispenser dispenser, BasePlayer player, Item item)
	{
		try
		{
			Interface.CallHook("OnDispenserGathered", dispenser, player, item);
		}
		catch (Exception ex)
		{
			Logger.Error("Staging OnDispenserGathered compatibility hook failed.", ex);
		}
	}

	private static Item OnDispenserBonusCompat(ResourceDispenser dispenser, BasePlayer player, Item item)
	{
		try
		{
			return Interface.CallHook("OnDispenserBonus", dispenser, player, item) is Item replacement ? replacement : item;
		}
		catch (Exception ex)
		{
			Logger.Error("Staging OnDispenserBonus compatibility hook failed.", ex);
			return item;
		}
	}

	private static void OnDispenserBonusReceivedCompat(ResourceDispenser dispenser, BasePlayer player, Item item)
	{
		try
		{
			Interface.CallHook("OnDispenserBonusReceived", dispenser, player, item);
		}
		catch (Exception ex)
		{
			Logger.Error("Staging OnDispenserBonusReceived compatibility hook failed.", ex);
		}
	}

	private static int OnItemCraftCompat(ItemCraftTask task, BasePlayer owner, Item fromTempBlueprint)
	{
		try
		{
			object result = Interface.CallHook("OnItemCraft", task, owner, fromTempBlueprint);

			if (result is not bool value)
			{
				return -1;
			}

			if (fromTempBlueprint != null && task?.instanceData != null)
			{
				fromTempBlueprint.instanceData = task.instanceData;
			}

			return value ? 1 : 0;
		}
		catch (Exception ex)
		{
			Logger.Error("Staging OnItemCraft compatibility hook failed.", ex);
			return -1;
		}
	}

	private static bool CallsNamed(CodeInstruction instruction, string methodName)
	{
		return instruction.operand is MethodBase method && method.Name == methodName;
	}

	private static bool CallsNamed(CodeInstruction instruction, Type declaringType, string methodName)
	{
		return instruction.operand is MethodBase method
		       && method.Name == methodName
		       && method.DeclaringType == declaringType;
	}

	private static int FindNextBranch(List<CodeInstruction> code, int startIndex, params OpCode[] opcodes)
	{
		if (startIndex < 0)
		{
			return -1;
		}

		for (int i = startIndex + 1; i < code.Count; i++)
		{
			foreach (OpCode opcode in opcodes)
			{
				if (code[i].opcode == opcode)
				{
					return i;
				}
			}
		}

		return -1;
	}

	private static bool IsBranch(CodeInstruction instruction, out Label label)
	{
		if (instruction.operand is Label branchLabel)
		{
			label = branchLabel;
			return true;
		}

		label = default;
		return false;
	}

	private static int FindLabelIndex(List<CodeInstruction> code, Label label)
	{
		int index = code.FindIndex(instruction => instruction.labels.Contains(label));

		if (index < 0)
		{
			throw new InvalidOperationException("Unable to find branch target label.");
		}

		return index;
	}

	private static object FindLastLeaveTarget(List<CodeInstruction> code)
	{
		for (int i = code.Count - 1; i >= 0; i--)
		{
			if ((code[i].opcode == OpCodes.Leave || code[i].opcode == OpCodes.Leave_S) && code[i].operand != null)
			{
				return code[i].operand;
			}
		}

		return null;
	}

	private static void MoveLabels(CodeInstruction from, CodeInstruction to)
	{
		if (from.labels.Count == 0)
		{
			return;
		}

		to.labels.AddRange(from.labels);
		from.labels.Clear();
	}

	private static CodeInstruction LoadArg(int index)
	{
		return index switch
		{
			0 => new CodeInstruction(OpCodes.Ldarg_0),
			1 => new CodeInstruction(OpCodes.Ldarg_1),
			2 => new CodeInstruction(OpCodes.Ldarg_2),
			3 => new CodeInstruction(OpCodes.Ldarg_3),
			_ => new CodeInstruction(OpCodes.Ldarg, index)
		};
	}

	private static CodeInstruction LoadLocal(int index)
	{
		return index switch
		{
			0 => new CodeInstruction(OpCodes.Ldloc_0),
			1 => new CodeInstruction(OpCodes.Ldloc_1),
			2 => new CodeInstruction(OpCodes.Ldloc_2),
			3 => new CodeInstruction(OpCodes.Ldloc_3),
			_ => new CodeInstruction(OpCodes.Ldloc, index)
		};
	}

	private static CodeInstruction StoreLocal(int index)
	{
		return index switch
		{
			0 => new CodeInstruction(OpCodes.Stloc_0),
			1 => new CodeInstruction(OpCodes.Stloc_1),
			2 => new CodeInstruction(OpCodes.Stloc_2),
			3 => new CodeInstruction(OpCodes.Stloc_3),
			_ => new CodeInstruction(OpCodes.Stloc, index)
		};
	}
}
#endif
