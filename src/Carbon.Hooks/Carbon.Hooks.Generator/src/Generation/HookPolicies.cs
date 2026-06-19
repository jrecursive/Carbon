using System.Text;
using Carbon.Projects.Oxide;
using Carbon.Utility;
using static Carbon.Projects.Oxide.Oxide;

namespace Carbon.Generation;

internal static class HookPolicies
{
	private const string OnClanCreatedKnownMsilHash = "635jwYcCfWiJfWKJFGaT8v7zfRJT0VND5GqMKxQMUVE=";
	private const string OnClanLogoChangedPatchKnownMsilHash = "KuTX1u22EO8+4GjS+SACsxt4g298bNL2E6omZgUevf8=";
	private const string OnPlayerAttackProjectileKnownMsilHash = "P9OGV+0jaDRBp1T6MGQoszFIqiq6mq3CbeIA/drU5UI=";
	private const string OnWireConnectKnownMsilHash = "NZXjvciMqiUrR0VTe/66AnlltciMZyNHLPA1Ajacumo=";

	public static bool MatchesOnClanCreatedAsyncSuccessRetargetPolicy(HookDef.Data hook)
	{
		return hook.HookName == "OnClanCreated"
		       && hook.Name == "OnClanCreated"
		       && hook.TypeName == "LocalClanBackend/<Create>d__11"
		       && hook.Signature.Name == "MoveNext";
	}

	public static bool MatchesOnTeamMemberInviteSendInviteAnchorPolicy(HookDef.Data hook)
	{
		return hook.HookName == "OnTeamMemberInvite"
		       && hook.Name == "OnTeamMemberInvite [sendinvite]"
		       && hook.TypeName == "RelationshipManager"
		       && hook.Signature.Name == "sendinvite";
	}

	public static bool MatchesOnClanLogoChangedPatchDependencyPolicy(HookDef.Data hook)
	{
		return hook.HookName == "OnClanLogoChanged [patch]"
		       && hook.Name == "OnClanLogoChanged [patch]"
		       && hook.TypeName == "LocalClan/<SetLogo>d__60"
		       && hook.Signature.Name == "MoveNext";
	}

	public static bool MatchesOnPlayerAttackProjectileLeavePolicy(HookDef.Data hook)
	{
		return hook.HookName == "OnPlayerAttack"
		       && hook.Name == "OnPlayerAttack [Projectile]"
		       && hook.TypeName == "BasePlayer"
		       && hook.Signature.Name == "OnProjectileAttack";
	}

	public static bool MatchesOnPlayerAttackProjectilePatchNoOpPolicy(HookDef.Data hook)
	{
		return hook.HookName == "OnPlayerAttack"
		       && hook.Name == "OnPlayerAttack [Projectile] [Patch]"
		       && hook.TypeName == "BasePlayer"
		       && hook.Signature.Name == "OnProjectileAttack";
	}

	public static bool MatchesCanCastFishingRodPolicy(HookDef.Data hook)
	{
		return hook.HookName == "CanCastFishingRod"
		       && hook.Name == "CanCastFishingRod"
		       && hook.TypeName == "BaseFishingRod"
		       && hook.Signature.Name == "Server_RequestCast";
	}

	public static bool MatchesCanCatchFishPolicy(HookDef.Data hook)
	{
		return hook.HookName == "CanCatchFish"
		       && hook.Name == "CanCatchFish"
		       && hook.TypeName == "BaseFishingRod"
		       && hook.Signature.Name == "CatchProcessBudgeted";
	}

	public static bool MatchesOnFishCatchPolicy(HookDef.Data hook)
	{
		return hook.HookName == "OnFishCatch"
		       && hook.Name == "OnFishCatch"
		       && hook.TypeName == "BaseFishingRod"
		       && hook.Signature.Name == "CatchProcessBudgeted";
	}

	public static bool MatchesOnCorpsePopulatePolicy(HookDef.Data hook)
	{
		return hook.HookName == "OnCorpsePopulate"
		       && hook.Name == "OnCorpsePopulate"
		       && hook.TypeName == "NPCPlayer"
		       && hook.Signature.Name == "CreateCorpse";
	}

	public static bool MatchesOnDispenserGatherPolicy(HookDef.Data hook)
	{
		return hook.HookName == "OnDispenserGather"
		       && hook.Name == "OnDispenserGather"
		       && hook.TypeName == "ResourceDispenser"
		       && hook.Signature.Name == "GiveResourceFromItem";
	}

	public static bool MatchesOnDispenserGatheredPolicy(HookDef.Data hook)
	{
		return hook.HookName == "OnDispenserGathered"
		       && hook.Name == "OnDispenserGathered"
		       && hook.TypeName == "ResourceDispenser"
		       && hook.Signature.Name == "GiveResourceFromItem";
	}

	public static bool MatchesOnDispenserBonusPolicy(HookDef.Data hook)
	{
		return hook.HookName == "OnDispenserBonus"
		       && hook.Name == "OnDispenserBonus"
		       && hook.TypeName == "ResourceDispenser"
		       && hook.Signature.Name == "AssignFinishBonus";
	}

	public static bool MatchesOnDispenserBonusReceivedPolicy(HookDef.Data hook)
	{
		return hook.HookName == "OnDispenserBonusReceived"
		       && hook.Name == "OnDispenserBonusReceived"
		       && hook.TypeName == "ResourceDispenser"
		       && hook.Signature.Name == "AssignFinishBonus";
	}

	public static bool MatchesOnCollectiblePickedupPolicy(HookDef.Data hook)
	{
		return hook.HookName == "OnCollectiblePickedup"
		       && hook.Name == "OnCollectiblePickedup"
		       && hook.TypeName == "CollectibleEntity"
		       && hook.Signature.Name == "DoPickup";
	}

	public static bool MatchesOnItemCraftPolicy(HookDef.Data hook)
	{
		return hook.HookName == "OnItemCraft"
		       && hook.Name == "OnItemCraft"
		       && hook.TypeName == "ItemCrafter"
		       && hook.Signature.Name == "CraftItem";
	}

	public static bool MatchesFixItemKeyIdObsoletePolicy(HookDef.Data hook)
	{
		return hook.HookName == "FixItemKeyId [patch]"
		       && hook.Name == "FixItemKeyId [patch]"
		       && hook.TypeName == "ItemCrafter"
		       && hook.Signature.Name == "CraftItem";
	}

	public static bool MatchesOnPlayerVoiceReadOnlySpanPolicy(HookDef.Data hook)
	{
		return hook.HookName == "OnPlayerVoice"
		       && hook.Name == "OnPlayerVoice"
		       && hook.TypeName == "BasePlayer"
		       && hook.Signature.Name == "OnReceivedVoice";
	}

	public static bool MatchesOnBonusItemDroppedObsoleteBranchPatchPolicy(HookDef.Data hook)
	{
		return hook.Name.StartsWith("OnBonusItemDropped [patch ", StringComparison.Ordinal)
		       && hook.TypeName == "LootContainer"
		       && hook.Signature.Name == "DropBonusItems";
	}

	public static bool MatchesFlameTurretTargetCleanupObsoletePolicy(HookDef.Data hook)
	{
		return hook.Name == "CanBeTargeted [FlameTurret] [cleanup]"
		       && hook.TypeName == "FlameTurret"
		       && hook.Signature.Name == "CheckTrigger";
	}

	public static bool MatchesFlameTurretCanBeTargetedLeavePolicy(HookDef.Data hook)
	{
		return hook.HookName == "CanBeTargeted"
		       && hook.Name == "CanBeTargeted [FlameTurret]"
		       && hook.TypeName == "FlameTurret"
		       && hook.Signature.Name == "CheckTrigger";
	}

	public static bool MatchesOnWireConnectStackSafePolicy(HookDef.Data hook)
	{
		return hook.HookName == "OnWireConnect"
		       && hook.Name == "OnWireConnect"
		       && hook.TypeName == "WireTool"
		       && hook.Signature.Name == "RPC_MakeConnection";
	}

	public static void WarnOnPolicyHashDrift(HookDef.Data hook)
	{
		if (MatchesOnClanCreatedAsyncSuccessRetargetPolicy(hook))
		{
			WarnOnPolicyHashDrift(hook, OnClanCreatedKnownMsilHash, nameof(TryGenerateOnClanCreatedAsyncSuccessRetargetPolicy));
		}

		if (MatchesOnClanLogoChangedPatchDependencyPolicy(hook))
		{
			WarnOnPolicyHashDrift(hook, OnClanLogoChangedPatchKnownMsilHash, nameof(MatchesOnClanLogoChangedPatchDependencyPolicy));
		}

		if (MatchesOnPlayerAttackProjectileLeavePolicy(hook))
		{
			WarnOnPolicyHashDrift(hook, OnPlayerAttackProjectileKnownMsilHash, nameof(TryGenerateOnPlayerAttackProjectileLeavePolicy));
		}

		if (MatchesOnWireConnectStackSafePolicy(hook))
		{
			WarnOnPolicyHashDrift(hook, OnWireConnectKnownMsilHash, nameof(TryGenerateOnWireConnectStackSafePolicy));
		}
	}

	public static bool ShouldEmitDependencyAttribute(HookDef.Data hook)
	{
		return !MatchesOnTeamMemberInviteSendInviteAnchorPolicy(hook)
		       && !MatchesOnClanLogoChangedPatchDependencyPolicy(hook);
	}

	public static string GetEmittedTargetTypeName(HookDef.Data hook)
	{
		return MatchesOnClanCreatedAsyncSuccessRetargetPolicy(hook) ? "LocalClanBackend" : hook.TypeName;
	}

	public static string GetEmittedTargetMethodName(HookDef.Data hook)
	{
		return MatchesOnClanCreatedAsyncSuccessRetargetPolicy(hook) ? "Create" : hook.Signature.Name;
	}

	public static string[] GetEmittedTargetMethodArgs(HookDef.Data hook)
	{
		if (MatchesOnClanCreatedAsyncSuccessRetargetPolicy(hook))
		{
			return ["System.UInt64", "System.String"];
		}

		if (MatchesOnPlayerVoiceReadOnlySpanPolicy(hook))
		{
			return ["System.ReadOnlySpan<System.Byte>"];
		}

		return hook.Signature.Parameters;
	}

	public static bool TryGeneratePolicyBody(StringBuilder body, HookDef.Data hook)
	{
		return TryGenerateOnClanCreatedAsyncSuccessRetargetPolicy(body, hook)
		       || TryGenerateOnPlayerVoiceReadOnlySpanPolicy(body, hook)
		       || TryGenerateCanCastFishingRodPolicy(body, hook)
		       || TryGenerateCanCatchFishPolicy(body, hook)
		       || TryGenerateOnFishCatchPolicy(body, hook)
		       || TryGenerateOnCorpsePopulatePolicy(body, hook)
		       || TryGenerateOnDispenserGatherPolicy(body, hook)
		       || TryGenerateOnDispenserGatheredPolicy(body, hook)
		       || TryGenerateOnDispenserBonusPolicy(body, hook)
		       || TryGenerateOnDispenserBonusReceivedPolicy(body, hook)
		       || TryGenerateOnCollectiblePickedupPolicy(body, hook)
		       || TryGenerateOnItemCraftPolicy(body, hook)
		       || TryGenerateOnPlayerAttackProjectileLeavePolicy(body, hook)
		       || TryGenerateFlameTurretCanBeTargetedLeavePolicy(body, hook)
		       || TryGenerateObsoleteNoOpTranspilerPolicy(body, hook)
		       || TryGenerateOnWireConnectStackSafePolicy(body, hook);
	}

	public static bool TryEmitModifyAnchorPrelude(StringBuilder body, HookDef.Data hook)
	{
		if (!MatchesOnTeamMemberInviteSendInviteAnchorPolicy(hook))
		{
			return false;
		}

		body.AppendLine(
			"var sendInvite = AccessTools.Method(Carbon.Extensions.AccessToolsEx.TypeByName(\"RelationshipManager+PlayerTeam\"), \"SendInvite\", new System.Type[] { Carbon.Extensions.AccessToolsEx.TypeByName(\"BasePlayer\") });");
		body.AppendLine("int anchorIndex = -1;");
		body.AppendLine("for (int i = 0; i < original.Count - 2; i++) {");
		body.AppendLine("if (original[i].opcode != OpCodes.Ldloc_1) { continue; }");
		body.AppendLine("if (original[i + 1].opcode != OpCodes.Ldloc_3) { continue; }");
		body.AppendLine("if (!Equals(original[i + 2].operand, sendInvite)) { continue; }");
		body.AppendLine("anchorIndex = i;");
		body.AppendLine("break;");
		body.AppendLine("}");
		body.AppendLine("if (anchorIndex < 0) return original.AsEnumerable();");
		return true;
	}

	public static void ConfigureModifyAnchorPolicy(HookDef.Data hook)
	{
		if (MatchesOnTeamMemberInviteSendInviteAnchorPolicy(hook))
		{
			Helper.UseModifyAnchor("anchorIndex", hook.InjectionIndex);
			return;
		}

		Helper.ClearModifyAnchor();
	}

	private static void WarnOnPolicyHashDrift(HookDef.Data hook, string expectedHash, string policyName)
	{
		if (string.IsNullOrWhiteSpace(hook.MsilHash) || hook.MsilHash == expectedHash)
		{
			return;
		}

		Logger.Warning($"{hook.HookName} policy '{policyName}' is applying with a drifted OPJ hash ({hook.MsilHash} != {expectedHash})");
	}

	private static bool TryGenerateOnClanCreatedAsyncSuccessRetargetPolicy(StringBuilder body, HookDef.Data hook)
	{
		if (!MatchesOnClanCreatedAsyncSuccessRetargetPolicy(hook))
		{
			return false;
		}

		Helper.Parameters.Add(("leaderSteamId", typeof(ulong)));
		body.AppendLine(
			"public static void Postfix(ulong leaderSteamId, ref System.Threading.Tasks.ValueTask<ClanValueResult<IClan>> __result) {");
		body.AppendLine("__result = AwaitHookResult(__result, leaderSteamId);");
		body.AppendLine("}");
		body.AppendLine(
			"private static async System.Threading.Tasks.ValueTask<ClanValueResult<IClan>> AwaitHookResult(System.Threading.Tasks.ValueTask<ClanValueResult<IClan>> original, ulong leaderSteamId) {");
		body.AppendLine("ClanValueResult<IClan> result = await original;");
		body.AppendLine("if (result.IsSuccess && result.Value is LocalClan clan) {");
		body.AppendLine($"HookCaller.CallStaticHook({HookStringPool.GetOrAdd(hook.HookName)}u, clan, leaderSteamId);");
		body.AppendLine("}");
		body.AppendLine("return result;");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine();
		return true;
	}

	private static bool TryGenerateOnPlayerVoiceReadOnlySpanPolicy(StringBuilder body, HookDef.Data hook)
	{
		if (!MatchesOnPlayerVoiceReadOnlySpanPolicy(hook))
		{
			return false;
		}

		Helper.Parameters.Add(("self", Tools.TypeByNameEx("BasePlayer") ?? typeof(object)));
		Helper.Parameters.Add(("data", typeof(byte[])));
		Helper.ReturnType = typeof(void);

		body.AppendLine("public static bool Prefix(BasePlayer __instance, ReadOnlySpan<byte> data) {");
		body.AppendLine("byte[] payload = data.Length == 0 ? Array.Empty<byte>() : data.ToArray();");
		body.AppendLine($"object result = HookCaller.CallStaticHook({HookStringPool.GetOrAdd(hook.HookName)}u, __instance, payload);");
		body.AppendLine("return result == null;");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine();
		return true;
	}

	private static bool TryGenerateCanCastFishingRodPolicy(StringBuilder body, HookDef.Data hook)
	{
		if (!MatchesCanCastFishingRodPolicy(hook))
		{
			return false;
		}

		Helper.Parameters.Add(("player", Tools.TypeByNameEx("BasePlayer") ?? typeof(object)));
		Helper.Parameters.Add(("self", Tools.TypeByNameEx("BaseFishingRod") ?? typeof(object)));
		Helper.Parameters.Add(("lure", Tools.TypeByNameEx("Item") ?? typeof(object)));
		Helper.Parameters.Add(("position", Tools.TypeByNameEx("UnityEngine.Vector3") ?? typeof(object)));
		Helper.ReturnType = typeof(bool);

		AppendTranspilerHelpers(body);
		body.AppendLine("private static bool ShouldCancelFishingCast(BasePlayer player, BaseFishingRod rod, Item lure, UnityEngine.Vector3 position) {");
		body.AppendLine($"return HookCaller.CallStaticHook({HookStringPool.GetOrAdd(hook.HookName)}u, player, rod, lure, position) is bool allowed && !allowed;");
		body.AppendLine("}");
		body.AppendLine("public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator, MethodBase Method) {");
		body.AppendLine("List<CodeInstruction> code = new List<CodeInstruction>(Instructions);");
		body.AppendLine("int evaluateCall = code.FindIndex(instruction => CallsNamed(instruction, \"EvaluateFishingPosition\"));");
		body.AppendLine("int branchIndex = evaluateCall >= 0 ? evaluateCall + 1 : -1;");
		body.AppendLine("if (branchIndex < 0 || branchIndex >= code.Count || !IsBranch(code[branchIndex], out Label continueCastLabel)) return code.AsEnumerable();");
		body.AppendLine("int insertIndex = FindLabelIndex(code, continueCastLabel);");
		body.AppendLine("if (insertIndex < 0) return code.AsEnumerable();");
		body.AppendLine("Label continueOriginal = Generator.DefineLabel();");
		body.AppendLine("List<CodeInstruction> insert = new List<CodeInstruction>();");
		body.AppendLine("insert.Add(LoadLocal(1));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldarg_0));");
		body.AppendLine("insert.Add(LoadLocal(2));");
		body.AppendLine("insert.Add(LoadLocal(0));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(MethodBase.GetCurrentMethod().DeclaringType, nameof(ShouldCancelFishingCast))));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Brfalse_S, continueOriginal));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ret));");
		body.AppendLine("MoveLabels(code[insertIndex], insert[0]);");
		body.AppendLine("code[insertIndex].labels.Add(continueOriginal);");
		body.AppendLine("code.InsertRange(insertIndex, insert);");
		body.AppendLine("return code.AsEnumerable();");
		body.AppendLine("}");
		CloseGeneratedHookBody(body);
		return true;
	}

	private static bool TryGenerateCanCatchFishPolicy(StringBuilder body, HookDef.Data hook)
	{
		if (!MatchesCanCatchFishPolicy(hook))
		{
			return false;
		}

		Helper.Parameters.Add(("player", Tools.TypeByNameEx("BasePlayer") ?? typeof(object)));
		Helper.Parameters.Add(("self", Tools.TypeByNameEx("BaseFishingRod") ?? typeof(object)));
		Helper.Parameters.Add(("item", Tools.TypeByNameEx("Item") ?? typeof(object)));
		Helper.ReturnType = typeof(bool);

		AppendTranspilerHelpers(body);
		body.AppendLine("private static bool ShouldCancelFishCatch(BasePlayer player, BaseFishingRod rod, Item item) {");
		body.AppendLine($"return HookCaller.CallStaticHook({HookStringPool.GetOrAdd(hook.HookName)}u, player, rod, item) is bool allowed && !allowed;");
		body.AppendLine("}");
		body.AppendLine("public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator, MethodBase Method) {");
		body.AppendLine("List<CodeInstruction> code = new List<CodeInstruction>(Instructions);");
		body.AppendLine("int ownershipCall = code.FindIndex(instruction => CallsNamed(instruction, typeof(Item), \"SetItemOwnership\"));");
		body.AppendLine("int popIndex = ownershipCall >= 0 && ownershipCall + 1 < code.Count && code[ownershipCall + 1].opcode == OpCodes.Pop ? ownershipCall + 1 : -1;");
		body.AppendLine("if (popIndex < 0) return code.AsEnumerable();");
		body.AppendLine("object leaveTarget = FindLastLeaveTarget(code);");
		body.AppendLine("if (leaveTarget == null) return code.AsEnumerable();");
		body.AppendLine("int insertIndex = popIndex + 1;");
		body.AppendLine("if (insertIndex >= code.Count) return code.AsEnumerable();");
		body.AppendLine("Label continueLabel = Generator.DefineLabel();");
		body.AppendLine("List<CodeInstruction> insert = new List<CodeInstruction>();");
		body.AppendLine("insert.Add(LoadLocal(2));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldarg_0));");
		body.AppendLine("insert.Add(LoadLocal(16));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(MethodBase.GetCurrentMethod().DeclaringType, nameof(ShouldCancelFishCatch))));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Brfalse_S, continueLabel));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Leave, leaveTarget));");
		body.AppendLine("code[insertIndex].labels.Add(continueLabel);");
		body.AppendLine("code.InsertRange(insertIndex, insert);");
		body.AppendLine("return code.AsEnumerable();");
		body.AppendLine("}");
		CloseGeneratedHookBody(body);
		return true;
	}

	private static bool TryGenerateOnFishCatchPolicy(StringBuilder body, HookDef.Data hook)
	{
		if (!MatchesOnFishCatchPolicy(hook))
		{
			return false;
		}

		Helper.Parameters.Add(("item", Tools.TypeByNameEx("Item") ?? typeof(object)));
		Helper.Parameters.Add(("self", Tools.TypeByNameEx("BaseFishingRod") ?? typeof(object)));
		Helper.Parameters.Add(("player", Tools.TypeByNameEx("BasePlayer") ?? typeof(object)));
		Helper.ReturnType = Tools.TypeByNameEx("Item") ?? typeof(object);

		AppendTranspilerHelpers(body);
		body.AppendLine("private static Item OnFishCatchCompat(Item item, BaseFishingRod rod, BasePlayer player) {");
		body.AppendLine($"return HookCaller.CallStaticHook({HookStringPool.GetOrAdd(hook.HookName)}u, item, rod, player) is Item replacement ? replacement : item;");
		body.AppendLine("}");
		body.AppendLine("private static bool TryFindAfterCanCatchFishBlock(List<CodeInstruction> code, int startIndex, out int insertIndex) {");
		body.AppendLine("insertIndex = startIndex;");
		body.AppendLine("int limit = Math.Min(code.Count, startIndex + 32);");
		body.AppendLine("for (int i = startIndex; i < limit; i++) {");
		body.AppendLine("if (!CallsNamed(code[i], \"ShouldCancelFishCatch\")) continue;");
		body.AppendLine("int candidate = i + 3;");
		body.AppendLine("if (candidate >= code.Count) return false;");
		body.AppendLine("insertIndex = candidate;");
		body.AppendLine("return true;");
		body.AppendLine("}");
		body.AppendLine("return false;");
		body.AppendLine("}");
		body.AppendLine("public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator, MethodBase Method) {");
		body.AppendLine("List<CodeInstruction> code = new List<CodeInstruction>(Instructions);");
		body.AppendLine("int ownershipCall = code.FindIndex(instruction => CallsNamed(instruction, typeof(Item), \"SetItemOwnership\"));");
		body.AppendLine("int popIndex = ownershipCall >= 0 && ownershipCall + 1 < code.Count && code[ownershipCall + 1].opcode == OpCodes.Pop ? ownershipCall + 1 : -1;");
		body.AppendLine("if (popIndex < 0) return code.AsEnumerable();");
		body.AppendLine("int insertIndex = popIndex + 1;");
		body.AppendLine("bool afterCanCatch = TryFindAfterCanCatchFishBlock(code, insertIndex, out insertIndex);");
		body.AppendLine("if (insertIndex >= code.Count) return code.AsEnumerable();");
		body.AppendLine("List<CodeInstruction> insert = new List<CodeInstruction>();");
		body.AppendLine("insert.Add(LoadLocal(16));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldarg_0));");
		body.AppendLine("insert.Add(LoadLocal(2));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(MethodBase.GetCurrentMethod().DeclaringType, nameof(OnFishCatchCompat))));");
		body.AppendLine("insert.Add(StoreLocal(16));");
		body.AppendLine("if (afterCanCatch) MoveLabels(code[insertIndex], insert[0]);");
		body.AppendLine("code.InsertRange(insertIndex, insert);");
		body.AppendLine("return code.AsEnumerable();");
		body.AppendLine("}");
		CloseGeneratedHookBody(body);
		return true;
	}

	private static bool TryGenerateOnCorpsePopulatePolicy(StringBuilder body, HookDef.Data hook)
	{
		if (!MatchesOnCorpsePopulatePolicy(hook))
		{
			return false;
		}

		Helper.Parameters.Add(("self", Tools.TypeByNameEx("NPCPlayer") ?? typeof(object)));
		Helper.Parameters.Add(("corpse", Tools.TypeByNameEx("LootableCorpse") ?? typeof(object)));
		Helper.ReturnType = typeof(void);

		body.AppendLine("public static void Postfix(NPCPlayer __instance, BaseCorpse __result) {");
		body.AppendLine("if (__instance == null || __result is not LootableCorpse corpse) return;");
		body.AppendLine($"HookCaller.CallStaticHook({HookStringPool.GetOrAdd(hook.HookName)}u, __instance, corpse);");
		body.AppendLine("}");
		CloseGeneratedHookBody(body);
		return true;
	}

	private static bool TryGenerateOnDispenserGatherPolicy(StringBuilder body, HookDef.Data hook)
	{
		if (!MatchesOnDispenserGatherPolicy(hook))
		{
			return false;
		}

		Helper.Parameters.Add(("self", Tools.TypeByNameEx("ResourceDispenser") ?? typeof(object)));
		Helper.Parameters.Add(("entity", Tools.TypeByNameEx("BasePlayer") ?? typeof(object)));
		Helper.Parameters.Add(("item", Tools.TypeByNameEx("Item") ?? typeof(object)));
		Helper.ReturnType = typeof(void);

		AppendTranspilerHelpers(body);
		body.AppendLine("private static bool ShouldSkipDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item) {");
		body.AppendLine($"return HookCaller.CallStaticHook({HookStringPool.GetOrAdd(hook.HookName)}u, dispenser, player, item) != null;");
		body.AppendLine("}");
		body.AppendLine("public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator, MethodBase Method) {");
		body.AppendLine("List<CodeInstruction> code = new List<CodeInstruction>(Instructions);");
		body.AppendLine("int createCall = code.FindIndex(instruction => CallsNamed(instruction, typeof(ItemManager), \"CreateByItemID\"));");
		body.AppendLine("int branchIndex = FindNextBranch(code, createCall, OpCodes.Brtrue, OpCodes.Brtrue_S);");
		body.AppendLine("if (branchIndex < 0 || !IsBranch(code[branchIndex], out Label itemCreatedLabel)) return code.AsEnumerable();");
		body.AppendLine("int insertIndex = FindLabelIndex(code, itemCreatedLabel);");
		body.AppendLine("if (insertIndex < 0) return code.AsEnumerable();");
		body.AppendLine("Label continueOriginal = Generator.DefineLabel();");
		body.AppendLine("List<CodeInstruction> insert = new List<CodeInstruction>();");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldarg_0));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldarg_1));");
		body.AppendLine("insert.Add(LoadLocal(7));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(MethodBase.GetCurrentMethod().DeclaringType, nameof(ShouldSkipDispenserGather))));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Brfalse_S, continueOriginal));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ret));");
		body.AppendLine("MoveLabels(code[insertIndex], insert[0]);");
		body.AppendLine("code[insertIndex].labels.Add(continueOriginal);");
		body.AppendLine("code.InsertRange(insertIndex, insert);");
		body.AppendLine("return code.AsEnumerable();");
		body.AppendLine("}");
		CloseGeneratedHookBody(body);
		return true;
	}

	private static bool TryGenerateOnDispenserGatheredPolicy(StringBuilder body, HookDef.Data hook)
	{
		if (!MatchesOnDispenserGatheredPolicy(hook))
		{
			return false;
		}

		Helper.Parameters.Add(("self", Tools.TypeByNameEx("ResourceDispenser") ?? typeof(object)));
		Helper.Parameters.Add(("entity", Tools.TypeByNameEx("BasePlayer") ?? typeof(object)));
		Helper.Parameters.Add(("item", Tools.TypeByNameEx("Item") ?? typeof(object)));
		Helper.ReturnType = typeof(void);

		AppendTranspilerHelpers(body);
		body.AppendLine("private static void OnDispenserGatheredCompat(ResourceDispenser dispenser, BasePlayer player, Item item) {");
		body.AppendLine($"HookCaller.CallStaticHook({HookStringPool.GetOrAdd(hook.HookName)}u, dispenser, player, item);");
		body.AppendLine("}");
		body.AppendLine("public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator, MethodBase Method) {");
		body.AppendLine("List<CodeInstruction> code = new List<CodeInstruction>(Instructions);");
		body.AppendLine("int analyticsCall = code.FindIndex(instruction => CallsNamed(instruction, \"OnGatherItem\"));");
		body.AppendLine("if (analyticsCall < 0) return code.AsEnumerable();");
		body.AppendLine("int insertIndex = analyticsCall + 1;");
		body.AppendLine("List<CodeInstruction> insert = new List<CodeInstruction>();");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldarg_0));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldarg_1));");
		body.AppendLine("insert.Add(LoadLocal(7));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(MethodBase.GetCurrentMethod().DeclaringType, nameof(OnDispenserGatheredCompat))));");
		body.AppendLine("code.InsertRange(insertIndex, insert);");
		body.AppendLine("return code.AsEnumerable();");
		body.AppendLine("}");
		CloseGeneratedHookBody(body);
		return true;
	}

	private static bool TryGenerateOnDispenserBonusPolicy(StringBuilder body, HookDef.Data hook)
	{
		if (!MatchesOnDispenserBonusPolicy(hook))
		{
			return false;
		}

		Helper.Parameters.Add(("self", Tools.TypeByNameEx("ResourceDispenser") ?? typeof(object)));
		Helper.Parameters.Add(("entity", Tools.TypeByNameEx("BasePlayer") ?? typeof(object)));
		Helper.Parameters.Add(("item", Tools.TypeByNameEx("Item") ?? typeof(object)));
		Helper.ReturnType = Tools.TypeByNameEx("Item") ?? typeof(object);

		AppendTranspilerHelpers(body);
		body.AppendLine("private static Item OnDispenserBonusCompat(ResourceDispenser dispenser, BasePlayer player, Item item) {");
		body.AppendLine($"return HookCaller.CallStaticHook({HookStringPool.GetOrAdd(hook.HookName)}u, dispenser, player, item) is Item replacement ? replacement : item;");
		body.AppendLine("}");
		body.AppendLine("public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator, MethodBase Method) {");
		body.AppendLine("List<CodeInstruction> code = new List<CodeInstruction>(Instructions);");
		body.AppendLine("int createCall = code.FindIndex(instruction => CallsNamed(instruction, typeof(ItemManager), \"Create\"));");
		body.AppendLine("int branchIndex = FindNextBranch(code, createCall, OpCodes.Brfalse, OpCodes.Brfalse_S);");
		body.AppendLine("if (branchIndex < 0 || !IsBranch(code[branchIndex], out Label noItemLabel)) return code.AsEnumerable();");
		body.AppendLine("int insertIndex = branchIndex + 1;");
		body.AppendLine("if (insertIndex >= code.Count) return code.AsEnumerable();");
		body.AppendLine("List<CodeInstruction> insert = new List<CodeInstruction>();");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldarg_0));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldarg_1));");
		body.AppendLine("insert.Add(LoadLocal(4));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(MethodBase.GetCurrentMethod().DeclaringType, nameof(OnDispenserBonusCompat))));");
		body.AppendLine("insert.Add(StoreLocal(4));");
		body.AppendLine("insert.Add(LoadLocal(4));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Brfalse_S, noItemLabel));");
		body.AppendLine("MoveLabels(code[insertIndex], insert[0]);");
		body.AppendLine("code.InsertRange(insertIndex, insert);");
		body.AppendLine("return code.AsEnumerable();");
		body.AppendLine("}");
		CloseGeneratedHookBody(body);
		return true;
	}

	private static bool TryGenerateOnDispenserBonusReceivedPolicy(StringBuilder body, HookDef.Data hook)
	{
		if (!MatchesOnDispenserBonusReceivedPolicy(hook))
		{
			return false;
		}

		Helper.Parameters.Add(("self", Tools.TypeByNameEx("ResourceDispenser") ?? typeof(object)));
		Helper.Parameters.Add(("entity", Tools.TypeByNameEx("BasePlayer") ?? typeof(object)));
		Helper.Parameters.Add(("item", Tools.TypeByNameEx("Item") ?? typeof(object)));
		Helper.ReturnType = typeof(void);

		AppendTranspilerHelpers(body);
		body.AppendLine("private static void OnDispenserBonusReceivedCompat(ResourceDispenser dispenser, BasePlayer player, Item item) {");
		body.AppendLine($"HookCaller.CallStaticHook({HookStringPool.GetOrAdd(hook.HookName)}u, dispenser, player, item);");
		body.AppendLine("}");
		body.AppendLine("public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator, MethodBase Method) {");
		body.AppendLine("List<CodeInstruction> code = new List<CodeInstruction>(Instructions);");
		body.AppendLine("int analyticsCall = code.FindIndex(instruction => CallsNamed(instruction, \"OnGatherItem\"));");
		body.AppendLine("if (analyticsCall < 0) return code.AsEnumerable();");
		body.AppendLine("int insertIndex = analyticsCall + 1;");
		body.AppendLine("List<CodeInstruction> insert = new List<CodeInstruction>();");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldarg_0));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldarg_1));");
		body.AppendLine("insert.Add(LoadLocal(4));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(MethodBase.GetCurrentMethod().DeclaringType, nameof(OnDispenserBonusReceivedCompat))));");
		body.AppendLine("code.InsertRange(insertIndex, insert);");
		body.AppendLine("return code.AsEnumerable();");
		body.AppendLine("}");
		CloseGeneratedHookBody(body);
		return true;
	}

	private static bool TryGenerateOnCollectiblePickedupPolicy(StringBuilder body, HookDef.Data hook)
	{
		if (!MatchesOnCollectiblePickedupPolicy(hook))
		{
			return false;
		}

		Helper.Parameters.Add(("self", Tools.TypeByNameEx("CollectibleEntity") ?? typeof(object)));
		Helper.Parameters.Add(("reciever", Tools.TypeByNameEx("BasePlayer") ?? typeof(object)));
		Helper.Parameters.Add(("item", Tools.TypeByNameEx("Item") ?? typeof(object)));
		Helper.ReturnType = typeof(void);

		AppendTranspilerHelpers(body);
		body.AppendLine("private static void OnCollectiblePickedupCompat(CollectibleEntity collectible, BasePlayer player, Item item) {");
		body.AppendLine($"HookCaller.CallStaticHook({HookStringPool.GetOrAdd(hook.HookName)}u, collectible, player, item);");
		body.AppendLine("}");
		body.AppendLine("private static bool IsCollectibleGiveItemCall(CodeInstruction instruction) {");
		body.AppendLine("if (instruction.operand is not MethodBase method || method.Name != \"GiveItem\" || method.DeclaringType != typeof(BasePlayer)) return false;");
		body.AppendLine("ParameterInfo[] parameters = method.GetParameters();");
		body.AppendLine("return parameters.Length >= 3 && parameters[0].ParameterType == typeof(Item);");
		body.AppendLine("}");
		body.AppendLine("private static bool TryGetLoadedLocalIndex(CodeInstruction instruction, out int index) {");
		body.AppendLine("index = -1;");
		body.AppendLine("if (instruction.opcode == OpCodes.Ldloc_0) { index = 0; return true; }");
		body.AppendLine("if (instruction.opcode == OpCodes.Ldloc_1) { index = 1; return true; }");
		body.AppendLine("if (instruction.opcode == OpCodes.Ldloc_2) { index = 2; return true; }");
		body.AppendLine("if (instruction.opcode == OpCodes.Ldloc_3) { index = 3; return true; }");
		body.AppendLine("if (instruction.opcode != OpCodes.Ldloc && instruction.opcode != OpCodes.Ldloc_S) return false;");
		body.AppendLine("if (instruction.operand is LocalBuilder builder) { index = builder.LocalIndex; return true; }");
		body.AppendLine("if (instruction.operand is int value) { index = value; return true; }");
		body.AppendLine("return false;");
		body.AppendLine("}");
		body.AppendLine("private static int FindItemLocalNearGiveItem(List<CodeInstruction> code, MethodBase method, int giveItemCall) {");
		body.AppendLine("IList<LocalVariableInfo> locals = method.GetMethodBody()?.LocalVariables;");
		body.AppendLine("if (locals == null) return -1;");
		body.AppendLine("for (int i = giveItemCall - 1; i >= Math.Max(0, giveItemCall - 8); i--) {");
		body.AppendLine("if (!TryGetLoadedLocalIndex(code[i], out int localIndex)) continue;");
		body.AppendLine("if (localIndex >= 0 && localIndex < locals.Count && locals[localIndex].LocalType == typeof(Item)) return localIndex;");
		body.AppendLine("}");
		body.AppendLine("return -1;");
		body.AppendLine("}");
		body.AppendLine("private static int FindGiveItemArgumentStart(List<CodeInstruction> code, int giveItemCall, int itemLocalIndex) {");
		body.AppendLine("for (int i = giveItemCall - 1; i >= Math.Max(0, giveItemCall - 8); i--) {");
		body.AppendLine("if (!TryGetLoadedLocalIndex(code[i], out int localIndex) || localIndex != itemLocalIndex) continue;");
		body.AppendLine("return Math.Max(0, i - 1);");
		body.AppendLine("}");
		body.AppendLine("return -1;");
		body.AppendLine("}");
		body.AppendLine("public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator, MethodBase Method) {");
		body.AppendLine("List<CodeInstruction> code = new List<CodeInstruction>(Instructions);");
		body.AppendLine("int giveItemCall = code.FindIndex(IsCollectibleGiveItemCall);");
		body.AppendLine("if (giveItemCall < 0) return code.AsEnumerable();");
		body.AppendLine("int itemLocalIndex = FindItemLocalNearGiveItem(code, Method, giveItemCall);");
		body.AppendLine("if (itemLocalIndex < 0) return code.AsEnumerable();");
		body.AppendLine("int insertIndex = FindGiveItemArgumentStart(code, giveItemCall, itemLocalIndex);");
		body.AppendLine("if (insertIndex < 0 || insertIndex >= code.Count) return code.AsEnumerable();");
		body.AppendLine("List<CodeInstruction> insert = new List<CodeInstruction>();");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldarg_0));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldarg_1));");
		body.AppendLine("insert.Add(LoadLocal(itemLocalIndex));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(MethodBase.GetCurrentMethod().DeclaringType, nameof(OnCollectiblePickedupCompat))));");
		body.AppendLine("MoveLabels(code[insertIndex], insert[0]);");
		body.AppendLine("code.InsertRange(insertIndex, insert);");
		body.AppendLine("return code.AsEnumerable();");
		body.AppendLine("}");
		CloseGeneratedHookBody(body);
		return true;
	}

	private static bool TryGenerateOnItemCraftPolicy(StringBuilder body, HookDef.Data hook)
	{
		if (!MatchesOnItemCraftPolicy(hook))
		{
			return false;
		}

		Helper.Parameters.Add(("task", Tools.TypeByNameEx("ItemCraftTask") ?? typeof(object)));
		Helper.Parameters.Add(("owner", Tools.TypeByNameEx("BasePlayer") ?? typeof(object)));
		Helper.Parameters.Add(("fromTempBlueprint", Tools.TypeByNameEx("Item") ?? typeof(object)));
		Helper.ReturnType = typeof(bool);

		AppendTranspilerHelpers(body);
		body.AppendLine("private static int OnItemCraftCompat(ItemCraftTask task, BasePlayer owner, Item fromTempBlueprint) {");
		body.AppendLine($"object result = HookCaller.CallStaticHook({HookStringPool.GetOrAdd(hook.HookName)}u, task, owner, fromTempBlueprint);");
		body.AppendLine("if (result is not bool value) return -1;");
		body.AppendLine("if (fromTempBlueprint != null && task != null && task.instanceData != null) fromTempBlueprint.instanceData = task.instanceData;");
		body.AppendLine("return value ? 1 : 0;");
		body.AppendLine("}");
		body.AppendLine("public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator, MethodBase Method) {");
		body.AppendLine("List<CodeInstruction> code = new List<CodeInstruction>(Instructions);");
		body.AppendLine("int addLastCall = code.FindIndex(instruction => CallsNamed(instruction, \"AddLast\"));");
		body.AppendLine("int insertIndex = addLastCall >= 3 ? addLastCall - 3 : -1;");
		body.AppendLine("if (insertIndex < 0 || insertIndex >= code.Count) return code.AsEnumerable();");
		body.AppendLine("LocalBuilder resultLocal = Generator.DeclareLocal(typeof(int));");
		body.AppendLine("Label continueOriginal = Generator.DefineLabel();");
		body.AppendLine("List<CodeInstruction> insert = new List<CodeInstruction>();");
		body.AppendLine("insert.Add(LoadLocal(0));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldarg_2));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldarg_S, 6));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(MethodBase.GetCurrentMethod().DeclaringType, nameof(OnItemCraftCompat))));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Stloc, resultLocal));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldloc, resultLocal));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldc_I4_M1));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Beq_S, continueOriginal));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldloc, resultLocal));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ldc_I4_1));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ceq));");
		body.AppendLine("insert.Add(new CodeInstruction(OpCodes.Ret));");
		body.AppendLine("MoveLabels(code[insertIndex], insert[0]);");
		body.AppendLine("code[insertIndex].labels.Add(continueOriginal);");
		body.AppendLine("code.InsertRange(insertIndex, insert);");
		body.AppendLine("return code.AsEnumerable();");
		body.AppendLine("}");
		CloseGeneratedHookBody(body);
		return true;
	}

	private static bool TryGenerateOnPlayerAttackProjectileLeavePolicy(StringBuilder body, HookDef.Data hook)
	{
		if (!MatchesOnPlayerAttackProjectileLeavePolicy(hook))
		{
			return false;
		}

		Helper.Parameters.Add(("self", Tools.TypeByNameEx("BasePlayer") ?? typeof(object)));
		Helper.Parameters.Add(("hitInfo", Tools.TypeByNameEx("HitInfo") ?? typeof(object)));
		Helper.ReturnType = typeof(void);

		body.AppendLine("private static CodeInstruction LoadLocal(int index) {");
		body.AppendLine("switch (index) {");
		body.AppendLine("case 0: return new CodeInstruction(OpCodes.Ldloc_0);");
		body.AppendLine("case 1: return new CodeInstruction(OpCodes.Ldloc_1);");
		body.AppendLine("case 2: return new CodeInstruction(OpCodes.Ldloc_2);");
		body.AppendLine("case 3: return new CodeInstruction(OpCodes.Ldloc_3);");
		body.AppendLine("default: return new CodeInstruction(OpCodes.Ldloc, index);");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator, MethodBase Method) {");
		body.AppendLine("List<CodeInstruction> original = new List<CodeInstruction>(Instructions);");
		body.AppendLine("var firedProjectileType = Carbon.Extensions.AccessToolsEx.TypeByName(\"BasePlayer+FiredProjectile\");");
		body.AppendLine("var lastEntityHitField = firedProjectileType == null ? null : AccessTools.Field(firedProjectileType, \"lastEntityHit\");");
		body.AppendLine("if (lastEntityHitField == null) return original.AsEnumerable();");
		body.AppendLine("int anchorIndex = -1;");
		body.AppendLine("for (int i = 2; i < original.Count; i++) {");
		body.AppendLine("if (original[i].opcode != OpCodes.Stfld || !Equals(original[i].operand, lastEntityHitField)) { continue; }");
		body.AppendLine("anchorIndex = i - 2;");
		body.AppendLine("break;");
		body.AppendLine("}");
		body.AppendLine("if (anchorIndex < 0) return original.AsEnumerable();");
		body.AppendLine("int returnIndex = original.FindLastIndex(instruction => instruction.opcode == OpCodes.Ret);");
		body.AppendLine("if (returnIndex < 0) return original.AsEnumerable();");
		body.AppendLine("var hitInfoType = Carbon.Extensions.AccessToolsEx.TypeByName(\"HitInfo\");");
		body.AppendLine("int hitInfoLocalIndex = Method.GetMethodBody()?.LocalVariables.FirstOrDefault(local => local.LocalType == hitInfoType)?.LocalIndex ?? 2;");
		body.AppendLine("bool isInsideExceptionBlock = IsInsideExceptionBlock(original, anchorIndex);");
		body.AppendLine("Label continueLabel = Generator.DefineLabel();");
		body.AppendLine("Label returnLabel = Generator.DefineLabel();");
		body.AppendLine("original[returnIndex].labels.Add(returnLabel);");
		body.AppendLine("var hookMethod = AccessTools.Method(typeof(HookCaller), nameof(HookCaller.CallStaticHook), new System.Type[] { typeof(uint), typeof(object), typeof(object) });");
		body.AppendLine("List<CodeInstruction> edit = new List<CodeInstruction>();");
		body.AppendLine($"edit.Add(new CodeInstruction(OpCodes.Ldc_I4, unchecked((int){HookStringPool.GetOrAdd(hook.HookName)}u)));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Ldarg_0));");
		body.AppendLine("edit.Add(LoadLocal(hitInfoLocalIndex));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Call, hookMethod));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Dup));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Brfalse_S, continueLabel));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Pop));");
		body.AppendLine("edit.Add(new CodeInstruction(isInsideExceptionBlock ? OpCodes.Leave : OpCodes.Br, returnLabel));");
		body.AppendLine("var continueInstruction = new CodeInstruction(OpCodes.Pop);");
		body.AppendLine("continueInstruction.labels.Add(continueLabel);");
		body.AppendLine("edit.Add(continueInstruction);");
		body.AppendLine("edit[0].MoveLabelsFrom(original[anchorIndex]).MoveBlocksFrom(original[anchorIndex]);");
		body.AppendLine("original.InsertRange(anchorIndex, edit);");
		body.AppendLine("return original.AsEnumerable();");
		body.AppendLine("}");
		body.AppendLine("private static bool IsInsideExceptionBlock(List<CodeInstruction> instructions, int index) {");
		body.AppendLine("int depth = 0;");
		body.AppendLine("for (int i = 0; i <= index && i < instructions.Count; i++) {");
		body.AppendLine("foreach (ExceptionBlock block in instructions[i].blocks) {");
		body.AppendLine("switch (block.blockType) {");
		body.AppendLine("case ExceptionBlockType.BeginExceptionBlock:");
		body.AppendLine("depth++;");
		body.AppendLine("break;");
		body.AppendLine("case ExceptionBlockType.EndExceptionBlock:");
		body.AppendLine("depth = Math.Max(0, depth - 1);");
		body.AppendLine("break;");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("return depth > 0;");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine();
		return true;
	}

	private static bool TryGenerateObsoleteNoOpTranspilerPolicy(StringBuilder body, HookDef.Data hook)
	{
		string reason;
		if (MatchesOnPlayerAttackProjectilePatchNoOpPolicy(hook))
		{
			reason = "Current staging OnPlayerAttack projectile policy owns cancellation; the stale OPJ dependency patch is obsolete.";
		}
		else if (MatchesFixItemKeyIdObsoletePolicy(hook))
		{
			reason = "Current staging ItemCrafter.CraftItem carries attachmentID natively; the stale key-id patch is obsolete.";
		}
		else if (MatchesOnBonusItemDroppedObsoleteBranchPatchPolicy(hook))
		{
			reason = "Current staging LootContainer.DropBonusItems no longer has the old post-hook branch target used by this OPJ patch.";
		}
		else if (MatchesFlameTurretTargetCleanupObsoletePolicy(hook))
		{
			reason = "Current staging FlameTurret.CheckTrigger already frees the raycast hit list in a finally block.";
		}
		else
		{
			return false;
		}

		body.AppendLine("public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions) {");
		body.AppendLine($"// {reason}");
		body.AppendLine("return Instructions;");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine();
		return true;
	}

	private static void AppendTranspilerHelpers(StringBuilder body)
	{
		body.AppendLine("private static CodeInstruction LoadLocal(int index) {");
		body.AppendLine("switch (index) {");
		body.AppendLine("case 0: return new CodeInstruction(OpCodes.Ldloc_0);");
		body.AppendLine("case 1: return new CodeInstruction(OpCodes.Ldloc_1);");
		body.AppendLine("case 2: return new CodeInstruction(OpCodes.Ldloc_2);");
		body.AppendLine("case 3: return new CodeInstruction(OpCodes.Ldloc_3);");
		body.AppendLine("default: return new CodeInstruction(OpCodes.Ldloc, index);");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("private static CodeInstruction StoreLocal(int index) {");
		body.AppendLine("switch (index) {");
		body.AppendLine("case 0: return new CodeInstruction(OpCodes.Stloc_0);");
		body.AppendLine("case 1: return new CodeInstruction(OpCodes.Stloc_1);");
		body.AppendLine("case 2: return new CodeInstruction(OpCodes.Stloc_2);");
		body.AppendLine("case 3: return new CodeInstruction(OpCodes.Stloc_3);");
		body.AppendLine("default: return new CodeInstruction(OpCodes.Stloc, index);");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("private static bool CallsNamed(CodeInstruction instruction, string methodName) {");
		body.AppendLine("return instruction.operand is MethodBase method && method.Name == methodName;");
		body.AppendLine("}");
		body.AppendLine("private static bool CallsNamed(CodeInstruction instruction, Type declaringType, string methodName) {");
		body.AppendLine("return instruction.operand is MethodBase method && method.Name == methodName && method.DeclaringType == declaringType;");
		body.AppendLine("}");
		body.AppendLine("private static bool IsBranch(CodeInstruction instruction, out Label label) {");
		body.AppendLine("if (instruction.operand is Label direct) { label = direct; return instruction.opcode.FlowControl == FlowControl.Branch || instruction.opcode.FlowControl == FlowControl.Cond_Branch; }");
		body.AppendLine("label = default;");
		body.AppendLine("return false;");
		body.AppendLine("}");
		body.AppendLine("private static int FindLabelIndex(List<CodeInstruction> instructions, Label label) {");
		body.AppendLine("return instructions.FindIndex(instruction => instruction.labels.Contains(label));");
		body.AppendLine("}");
		body.AppendLine("private static int FindNextBranch(List<CodeInstruction> instructions, int startIndex, params OpCode[] opcodes) {");
		body.AppendLine("if (startIndex < 0) return -1;");
		body.AppendLine("for (int i = startIndex + 1; i < instructions.Count; i++) {");
		body.AppendLine("if (opcodes.Contains(instructions[i].opcode)) return i;");
		body.AppendLine("}");
		body.AppendLine("return -1;");
		body.AppendLine("}");
		body.AppendLine("private static object FindLastLeaveTarget(List<CodeInstruction> instructions) {");
		body.AppendLine("for (int i = instructions.Count - 1; i >= 0; i--) {");
		body.AppendLine("if (instructions[i].opcode == OpCodes.Leave || instructions[i].opcode == OpCodes.Leave_S) return instructions[i].operand;");
		body.AppendLine("}");
		body.AppendLine("return null;");
		body.AppendLine("}");
		body.AppendLine("private static void MoveLabels(CodeInstruction from, CodeInstruction to) {");
		body.AppendLine("to.labels.AddRange(from.labels);");
		body.AppendLine("from.labels.Clear();");
		body.AppendLine("to.blocks.AddRange(from.blocks);");
		body.AppendLine("from.blocks.Clear();");
		body.AppendLine("}");
	}

	private static void CloseGeneratedHookBody(StringBuilder body)
	{
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine();
	}

	private static bool TryGenerateFlameTurretCanBeTargetedLeavePolicy(StringBuilder body, HookDef.Data hook)
	{
		if (!MatchesFlameTurretCanBeTargetedLeavePolicy(hook))
		{
			return false;
		}

		Helper.Parameters.Add(("local7", Tools.TypeByNameEx("BasePlayer") ?? typeof(object)));
		Helper.Parameters.Add(("self", Tools.TypeByNameEx("FlameTurret") ?? typeof(object)));
		Helper.ReturnType = typeof(bool);

		body.AppendLine("public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator, MethodBase Method) {");
		body.AppendLine("List<CodeInstruction> original = new List<CodeInstruction>(Instructions);");
		body.AppendLine($"int anchorIndex = System.Math.Min({hook.InjectionIndex}, original.Count - 1);");
		body.AppendLine("int retIndex = original.FindLastIndex(instruction => instruction.opcode == OpCodes.Ret);");
		body.AppendLine("if (anchorIndex < 0 || retIndex <= 0) return original.AsEnumerable();");
		body.AppendLine("int returnLoadIndex = retIndex - 1;");
		body.AppendLine("Label continueLabel = Generator.DefineLabel();");
		body.AppendLine("Label returnLabel = Generator.DefineLabel();");
		body.AppendLine("original[returnLoadIndex].labels.Add(returnLabel);");
		body.AppendLine("var hookMethod = AccessTools.Method(typeof(HookCaller), nameof(HookCaller.CallStaticHook), new System.Type[] { typeof(uint), typeof(object), typeof(object) });");
		body.AppendLine("List<CodeInstruction> edit = new List<CodeInstruction>();");
		body.AppendLine($"edit.Add(new CodeInstruction(OpCodes.Ldc_I4, unchecked((int){HookStringPool.GetOrAdd(hook.HookName)}u)));");
		body.AppendLine("edit.Add(__GeneratorRuntime.CreateLoadLocalInstruction(Generator, Method, 7, Carbon.Extensions.AccessToolsEx.TypeByName(\"BasePlayer\")));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Ldarg_0));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Call, hookMethod));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Dup));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Isinst, typeof(bool)));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Brfalse_S, continueLabel));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Unbox_Any, typeof(bool)));");
		body.AppendLine("edit.Add(__GeneratorRuntime.CreateStoreLocalInstruction(Generator, Method, 5, typeof(bool)));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Leave, returnLabel));");
		body.AppendLine("var continueInstruction = new CodeInstruction(OpCodes.Pop);");
		body.AppendLine("continueInstruction.labels.Add(continueLabel);");
		body.AppendLine("edit.Add(continueInstruction);");
		body.AppendLine("edit[0].MoveLabelsFrom(original[anchorIndex]).MoveBlocksFrom(original[anchorIndex]);");
		body.AppendLine("original.InsertRange(anchorIndex, edit);");
		body.AppendLine("return original.AsEnumerable();");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine();
		return true;
	}

	private static bool TryGenerateOnWireConnectStackSafePolicy(StringBuilder body, HookDef.Data hook)
	{
		if (!MatchesOnWireConnectStackSafePolicy(hook))
		{
			return false;
		}

		var wireConnectionMessageType = Tools.TypeByNameEx("ProtoBuf.WireConnectionMessage");
		var linePointsType = wireConnectionMessageType?.GetField("linePoints")?.FieldType ?? typeof(object);
		var slackLevelsType = wireConnectionMessageType?.GetField("slackLevels")?.FieldType ?? typeof(object);

		Helper.Parameters.Add(("local0", Tools.TypeByNameEx("BasePlayer") ?? typeof(object)));
		Helper.Parameters.Add(("local5", Tools.TypeByNameEx("IOEntity") ?? typeof(object)));
		Helper.Parameters.Add(("local3", typeof(int)));
		Helper.Parameters.Add(("local6", Tools.TypeByNameEx("IOEntity") ?? typeof(object)));
		Helper.Parameters.Add(("local4", typeof(int)));
		Helper.Parameters.Add(("linePoints", linePointsType));
		Helper.Parameters.Add(("local8", slackLevelsType));
		Helper.ReturnType = typeof(void);

		body.AppendLine("public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator, MethodBase Method) {");
		body.AppendLine("List<CodeInstruction> original = new List<CodeInstruction>(Instructions);");
		body.AppendLine("var wireToolType = Carbon.Extensions.AccessToolsEx.TypeByName(\"WireTool\");");
		body.AppendLine("var wireMessageType = Carbon.Extensions.AccessToolsEx.TypeByName(\"ProtoBuf.WireConnectionMessage\");");
		body.AppendLine("var intToColourMethod = AccessTools.Method(wireToolType, \"IntToColour\", new System.Type[] { typeof(int) });");
		body.AppendLine("var wireColorField = AccessTools.Field(wireMessageType, \"wireColor\");");
		body.AppendLine("var linePointsField = AccessTools.Field(wireMessageType, \"linePoints\");");
		body.AppendLine("if (intToColourMethod == null || wireColorField == null || linePointsField == null) return original.AsEnumerable();");
		body.AppendLine("int anchorIndex = -1;");
		body.AppendLine("for (int i = 3; i < original.Count; i++) {");
		body.AppendLine("if (original[i].opcode != OpCodes.Call || !Equals(original[i].operand, intToColourMethod)) { continue; }");
		body.AppendLine("if (original[i - 3].opcode != OpCodes.Ldarg_0) { continue; }");
		body.AppendLine("if (original[i - 2].opcode != OpCodes.Ldloc_1) { continue; }");
		body.AppendLine("if (original[i - 1].opcode != OpCodes.Ldfld || !Equals(original[i - 1].operand, wireColorField)) { continue; }");
		body.AppendLine("anchorIndex = i - 3;");
		body.AppendLine("break;");
		body.AppendLine("}");
		body.AppendLine("if (anchorIndex < 0) return original.AsEnumerable();");
		body.AppendLine("Label continueLabel = Generator.DefineLabel();");
		body.AppendLine("var hookMethod = AccessTools.Method(typeof(HookCaller), nameof(HookCaller.CallStaticHook), new System.Type[] { typeof(uint), typeof(object), typeof(object), typeof(object), typeof(object), typeof(object), typeof(object), typeof(object) });");
		body.AppendLine("List<CodeInstruction> edit = new List<CodeInstruction>();");
		body.AppendLine($"edit.Add(new CodeInstruction(OpCodes.Ldc_I4, unchecked((int){HookStringPool.GetOrAdd(hook.HookName)}u)));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Ldloc_0));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Ldloc_S, 5));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Ldloc_3));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Box, typeof(int)));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Ldloc_S, 6));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Ldloc_S, 4));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Box, typeof(int)));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Ldloc_1));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Ldfld, linePointsField));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Ldloc_S, 8));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Call, hookMethod));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Dup));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Brfalse_S, continueLabel));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Pop));");
		body.AppendLine("edit.Add(new CodeInstruction(OpCodes.Ret));");
		body.AppendLine("var continueInstruction = new CodeInstruction(OpCodes.Pop);");
		body.AppendLine("continueInstruction.labels.Add(continueLabel);");
		body.AppendLine("edit.Add(continueInstruction);");
		body.AppendLine("edit[0].MoveLabelsFrom(original[anchorIndex]).MoveBlocksFrom(original[anchorIndex]);");
		body.AppendLine("original.InsertRange(anchorIndex, edit);");
		body.AppendLine("return original.AsEnumerable();");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine("}");
		body.AppendLine();
		return true;
	}
}
