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
		if (MatchesOnBonusItemDroppedObsoleteBranchPatchPolicy(hook))
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
