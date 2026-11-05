using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static class NativeVetoAdmissionSemantics
{
    internal const uint CrateId = 1229062350u;
    internal const uint PetId = 1722860509u;

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Verify(IReadOnlyList<CodeInstruction> il, MethodBase target)
    {
        bool crate = target.DeclaringType?.FullName == "HackableLockedCrate" && target.Name == "RPC_Hack";
        bool pet = target.DeclaringType?.FullName == "FrankensteinTable" && target.Name == "SleepFrankenstein";
        Require((crate || pet) && target is MethodInfo method && method.ReturnType == typeof(void) &&
                target.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(
                    new[] { crate ? "BaseEntity+RPCMessage" : "BasePlayer" }),
                "Native veto must retain the exact crate RPC or Frankenstein sleep signature.");
        List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(target, out _);
        int injection = crate ? 7 : 41;
        Require(original.Count == (crate ? 35 : 53) && target.GetMethodBody()!.ExceptionHandlingClauses.Count == 0,
                "Native veto target control flow changed; review its admission boundary.");
        if (crate)
        {
            Require(Call(original[1], "IsBeingHacked") && Call(original[4], "IsFullyHacked") &&
                    original[2].opcode == OpCodes.Brtrue_S && Target(original, original[2].operand) == 6 &&
                    original[5].opcode == OpCodes.Brfalse_S && Target(original, original[5].operand) == injection &&
                    original[6].opcode == OpCodes.Ret && Call(original[10], "OnLockedCrateStarted") &&
                    original[16].opcode == OpCodes.Stfld && original[16].operand is FieldInfo field && field.Name == "originalHackerPlayerId" &&
                    Call(original[30], "GiveAchievement") && Call(original[33], "StartHacking"),
                    "Crate admission must follow both native guards and precede analytics, owner, achievement and hacking effects.");
        }
        else
        {
            Require(target.GetMethodBody()!.LocalVariables.Count == 1 &&
                    target.GetMethodBody()!.LocalVariables[0].LocalType.FullName == "FrankensteinPet" &&
                    Call(original[1], "IsInventoryEmpty") && Call(original[13], "CanBuild") &&
                    Call(original[37], "Distance") && original[39].opcode == OpCodes.Blt_Un_S &&
                    Target(original, original[39].operand) == injection && original[40].opcode == OpCodes.Ret &&
                    Call(original[43], "ReturnFrankensteinItems") && Call(original[45], "DoRemoves") &&
                    Call(original[47], "SendNetworkUpdateImmediate") && Call(original[51], "Kill"),
                    "Pet admission must follow native eligibility/distance guards and precede item, network and kill effects.");
        }

        int[] dispatches = Enumerable.Range(0, il.Count).Where(i => Call(il[i], "CallStaticHook")).ToArray();
        Require(dispatches.Length == 1, "Native veto must dispatch exactly one hook.");
        int call = dispatches[0], start = call - 4, continuation = call + 4;
        Require(start == injection && il.Count == original.Count + 8 && il[start].opcode == OpCodes.Ldc_I4 &&
                Equals(il[start].operand, unchecked((int)(crate ? CrateId : PetId))),
                "Native veto must enter with an empty stack at the reviewed admission boundary.");
        MethodInfo dispatch = (MethodInfo)il[call].operand;
        Type[] dispatchTypes = crate ? [typeof(uint), typeof(object), typeof(object)] :
            [typeof(uint), typeof(object), typeof(object), typeof(object)];
        Require(dispatch.ReturnType == typeof(object) && dispatch.GetParameters().Select(p => p.ParameterType).SequenceEqual(dispatchTypes),
                "Native veto callback return/argument ABI changed.");
        if (crate)
            Require(il[start + 1].opcode == OpCodes.Ldarg_1 && il[start + 2].opcode == OpCodes.Ldfld &&
                    il[start + 2].operand is FieldInfo player && player.Name == "player" &&
                    player.DeclaringType?.FullName == "BaseEntity+RPCMessage" && player.FieldType.FullName == "BasePlayer" &&
                    il[start + 3].opcode == OpCodes.Ldarg_0,
                    "Crate callback must retain RPC player, then exact crate identity.");
        else
            Require(il[start + 1].opcode == OpCodes.Ldloc_0 && il[start + 2].opcode == OpCodes.Ldarg_0 &&
                    il[start + 3].opcode == OpCodes.Ldarg_1,
                    "Pet callback must retain resolved pet, table and owner identity.");
        Require(il[call + 1].opcode == OpCodes.Ldnull &&
                (il[call + 2].opcode == OpCodes.Beq || il[call + 2].opcode == OpCodes.Beq_S) &&
                Target(il, il[call + 2].operand) == continuation && il[call + 3].opcode == OpCodes.Ret,
                "Only null may continue native effects; every non-null result, including boxed false, must return.");

        // Remove only the injected block conceptually. Every original opcode,
        // operand and branch must survive; native branches to admission must
        // enter the hook, while the null result alone enters its continuation.
        for (int i = 0; i < original.Count; i++)
        {
            CodeInstruction before = original[i], after = il[i < injection ? i : i + 8];
            Require(before.opcode == after.opcode, "Native veto changed an original instruction.");
            if (before.operand is Label label)
            {
                int oldTarget = Target(original, label);
                int expected = oldTarget <= injection ? oldTarget : oldTarget + 8;
                Require(Target(il, after.operand) == expected, "Native guard branch bypasses admission or changes native flow.");
            }
            else if (before.operand is LocalBuilder local)
                Require(after.operand is LocalBuilder rewritten && local.LocalIndex == rewritten.LocalIndex,
                        "Native veto changed a local operand.");
            else
                Require(Equals(before.operand, after.operand), "Native veto changed an original operand or effect.");
        }
    }

    private static bool Call(CodeInstruction instruction, string name) =>
        (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
        instruction.operand is MethodInfo method && method.Name == name;

    private static int Target(IReadOnlyList<CodeInstruction> il, object operand)
    {
        if (operand is not Label label) throw new InvalidOperationException("Native veto branch lacks a resolved label.");
        int[] matches = Enumerable.Range(0, il.Count).Where(i => il[i].labels.Contains(label)).ToArray();
        Require(matches.Length == 1, "Native veto branch must resolve to exactly one instruction.");
        return matches[0];
    }
}
