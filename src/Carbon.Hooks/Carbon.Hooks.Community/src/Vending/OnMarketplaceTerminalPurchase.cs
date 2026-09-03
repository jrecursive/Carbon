using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System;
using API.Hooks;
using HarmonyLib;
using Patch = API.Hooks.Patch;

namespace Carbon.Hooks;

#pragma warning disable IDE0051

public partial class Category_Vending
{
	public partial class Vending_MarketTerminal
	{
		[HookAttribute.Patch("OnMarketplaceTerminalPurchase", "OnMarketplaceTerminalPurchase", typeof(MarketTerminal), "Server_Purchase", new System.Type[] { typeof(BaseEntity.RPCMessage) })]

		[MetadataAttribute.Category("Vending")]
		[MetadataAttribute.Parameter("terminal", typeof(MarketTerminal))]
		[MetadataAttribute.Parameter("vending", typeof(VendingMachine))]
		[MetadataAttribute.Parameter("player", typeof(BasePlayer))]
		[MetadataAttribute.Parameter("sellOrderIndex", typeof(int))]
		[MetadataAttribute.Parameter("amount", typeof(int))]
		[MetadataAttribute.Info("Called before making a purchase at the Marketplace terminal.")]
		[MetadataAttribute.Return(typeof(void))]

		public class OnMarketplaceTerminalPurchase : Patch
		{
			public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator, MethodBase Method)
			{
				var original = new List<CodeInstruction>(Instructions);
				var deliveryEligibility = AccessTools.Method(
					typeof(MarketTerminal),
					nameof(MarketTerminal.GetDeliveryEligibleVendingMachines),
					new[] { typeof(List<NetworkableId>) });
				var readInt32 = AccessTools.Method(typeof(Network.NetRead), nameof(Network.NetRead.Int32));
				var rpcPlayer = AccessTools.Field(typeof(BaseEntity.RPCMessage), nameof(BaseEntity.RPCMessage.player));
				var hook = AccessTools.Method(
					typeof(HookCaller),
					nameof(HookCaller.CallStaticHook),
					new[] { typeof(uint), typeof(object), typeof(object), typeof(object), typeof(object), typeof(object) });

				if (deliveryEligibility == null || readInt32 == null || rpcPlayer == null || hook == null)
				{
					throw new InvalidOperationException("Marketplace purchase hook dependencies could not be resolved.");
				}

				var eligibilityCalls = FindCalls(original, deliveryEligibility);
				if (eligibilityCalls.Count != 1)
				{
					throw new InvalidOperationException(
						$"Expected one MarketTerminal.GetDeliveryEligibleVendingMachines call, found {eligibilityCalls.Count}.");
				}

				var eligibilityCallIndex = eligibilityCalls[0];
				var nullIndex = PreviousMeaningfulInstruction(original, eligibilityCallIndex);
				var anchorIndex = PreviousMeaningfulInstruction(original, nullIndex);
				if (nullIndex < 0 || original[nullIndex].opcode != OpCodes.Ldnull ||
					anchorIndex < 0 || original[anchorIndex].opcode != OpCodes.Ldarg_0)
				{
					throw new InvalidOperationException(
						"Marketplace delivery-eligibility call no longer has the expected stack-empty ldarg.0/ldnull prelude.");
				}

				var body = Method.GetMethodBody() ?? throw new InvalidOperationException("Marketplace purchase target has no method body.");
				var vendingLocal = FindUniqueLocal(body, typeof(VendingMachine));
				var intReadLocals = FindStoredLocalsAfterCalls(original, readInt32, eligibilityCallIndex);
				if (intReadLocals.Count != 2 ||
					body.LocalVariables[intReadLocals[0]].LocalType != typeof(int) ||
					body.LocalVariables[intReadLocals[1]].LocalType != typeof(int))
				{
					throw new InvalidOperationException(
						$"Expected two Int32 RPC read locals before marketplace eligibility, found {intReadLocals.Count}.");
				}

				var continueLabel = Generator.DefineLabel();
				var edit = new List<CodeInstruction>
				{
					new CodeInstruction(OpCodes.Ldc_I4, unchecked((int)2145652880)),
					new CodeInstruction(OpCodes.Ldarg_0),
					LoadLocal(vendingLocal),
					new CodeInstruction(OpCodes.Ldarg_1),
					new CodeInstruction(OpCodes.Ldfld, rpcPlayer),
					LoadLocal(intReadLocals[0]),
					new CodeInstruction(OpCodes.Box, typeof(int)),
					LoadLocal(intReadLocals[1]),
					new CodeInstruction(OpCodes.Box, typeof(int)),
					new CodeInstruction(OpCodes.Call, hook),
					new CodeInstruction(OpCodes.Brfalse, continueLabel),
					new CodeInstruction(OpCodes.Ret)
				};

				edit[0].MoveLabelsFrom(original[anchorIndex]).MoveBlocksFrom(original[anchorIndex]);
				original[anchorIndex].labels.Add(continueLabel);
				original.InsertRange(anchorIndex, edit);
				return original;
			}

			private static List<int> FindCalls(IReadOnlyList<CodeInstruction> instructions, MethodInfo method)
			{
				var result = new List<int>();
				for (var i = 0; i < instructions.Count; i++)
				{
					if ((instructions[i].opcode == OpCodes.Call || instructions[i].opcode == OpCodes.Callvirt) &&
						Equals(instructions[i].operand, method))
					{
						result.Add(i);
					}
				}

				return result;
			}

			private static int FindUniqueLocal(MethodBody body, Type type)
			{
				var found = -1;
				foreach (var local in body.LocalVariables)
				{
					if (local.LocalType != type)
					{
						continue;
					}

					if (found >= 0)
					{
						throw new InvalidOperationException($"Expected one {type.FullName} local in marketplace purchase target.");
					}

					found = local.LocalIndex;
				}

				return found >= 0
					? found
					: throw new InvalidOperationException($"No {type.FullName} local was found in marketplace purchase target.");
			}

			private static List<int> FindStoredLocalsAfterCalls(
				IReadOnlyList<CodeInstruction> instructions,
				MethodInfo method,
				int beforeIndex)
			{
				var result = new List<int>();
				for (var i = 0; i < beforeIndex; i++)
				{
					if ((instructions[i].opcode != OpCodes.Call && instructions[i].opcode != OpCodes.Callvirt) ||
						!Equals(instructions[i].operand, method))
					{
						continue;
					}

					var storeIndex = NextMeaningfulInstruction(instructions, i);
					if (storeIndex < 0 || !TryGetStoredLocal(instructions[storeIndex], out var localIndex))
					{
						throw new InvalidOperationException("Marketplace RPC Int32 read is no longer stored directly into a local.");
					}

					result.Add(localIndex);
				}

				return result;
			}

			private static int PreviousMeaningfulInstruction(IReadOnlyList<CodeInstruction> instructions, int beforeIndex)
			{
				for (var i = beforeIndex - 1; i >= 0; i--)
				{
					if (instructions[i].opcode != OpCodes.Nop)
					{
						return i;
					}
				}

				return -1;
			}

			private static int NextMeaningfulInstruction(IReadOnlyList<CodeInstruction> instructions, int afterIndex)
			{
				for (var i = afterIndex + 1; i < instructions.Count; i++)
				{
					if (instructions[i].opcode != OpCodes.Nop)
					{
						return i;
					}
				}

				return -1;
			}

			private static bool TryGetStoredLocal(CodeInstruction instruction, out int localIndex)
			{
				if (instruction.opcode == OpCodes.Stloc_0) localIndex = 0;
				else if (instruction.opcode == OpCodes.Stloc_1) localIndex = 1;
				else if (instruction.opcode == OpCodes.Stloc_2) localIndex = 2;
				else if (instruction.opcode == OpCodes.Stloc_3) localIndex = 3;
				else if ((instruction.opcode == OpCodes.Stloc || instruction.opcode == OpCodes.Stloc_S) && instruction.operand is LocalBuilder local)
					localIndex = local.LocalIndex;
				else if ((instruction.opcode == OpCodes.Stloc || instruction.opcode == OpCodes.Stloc_S) && instruction.operand is int index)
					localIndex = index;
				else
				{
					localIndex = -1;
					return false;
				}

				return true;
			}

			private static CodeInstruction LoadLocal(int localIndex)
			{
				return localIndex switch
				{
					0 => new CodeInstruction(OpCodes.Ldloc_0),
					1 => new CodeInstruction(OpCodes.Ldloc_1),
					2 => new CodeInstruction(OpCodes.Ldloc_2),
					3 => new CodeInstruction(OpCodes.Ldloc_3),
					_ => new CodeInstruction(OpCodes.Ldloc, localIndex)
				};
			}
		}
	}
}
