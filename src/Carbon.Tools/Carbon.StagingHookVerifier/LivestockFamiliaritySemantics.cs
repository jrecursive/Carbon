using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static class LivestockFamiliaritySemantics
{
    internal const uint AddingId = 2189779015u;
    internal const uint AddedId = 3837166704u;

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static int Dispatch(IReadOnlyList<CodeInstruction> il, uint id)
    {
        int[] calls = Enumerable.Range(0, il.Count).Where(i => il[i].operand is MethodInfo method && method.Name == "CallStaticHook").ToArray();
        Require(calls.Length == 1, "Familiarity patch must dispatch exactly one callback.");
        int call = calls[0];
        Require(call >= 6 && il[call - 6].opcode == OpCodes.Ldc_I4 && Equals(il[call - 6].operand, unchecked((int)id)) &&
                il[call - 5].opcode == OpCodes.Ldarg_0 && il[call - 4].opcode == OpCodes.Ldarg_1 &&
                il[call - 3].opcode == OpCodes.Box && Equals(il[call - 3].operand, typeof(ulong)) &&
                il[call - 2].opcode == OpCodes.Ldarg_2 && il[call - 1].opcode == OpCodes.Box &&
                Equals(il[call - 1].operand, typeof(float)),
                "Familiarity callback must retain animal/userId/seconds argument identity and exact scalar boxing.");
        MethodInfo dispatch = (MethodInfo)il[call].operand;
        Require(dispatch.ReturnType == typeof(object) && dispatch.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                new[] { typeof(uint), typeof(object), typeof(object), typeof(object) }),
                "Familiarity callbacks must use the available three-argument native contract.");
        return call;
    }

    internal static void Verify(MethodBase target, MethodInfo prefix, MethodInfo postfix)
    {
        Require(target.DeclaringType?.FullName == "Rust.Ai.Gen2.LivestockAnimal" && target.Name == "AddFamiliarity" &&
                target.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(ulong), typeof(float) }),
                "Familiarity hooks must retain the exact current two-argument native target.");
        Require(PatchProcessor.GetOriginalInstructions(target, out _).Count(i => i.opcode == OpCodes.Ret) == 6,
                "Native familiarity return paths changed; review completion coverage.");
        Require(prefix.ReturnType == typeof(bool) && prefix.GetParameters().Select(p => (p.Name ?? string.Empty, p.ParameterType)).SequenceEqual(
                new[] { ("__instance", typeof(object)), ("userId", typeof(ulong)), ("seconds", typeof(float)) }),
                "Familiarity admission must use a bool prefix with the exact native arguments.");
        Require(postfix.ReturnType == typeof(void) && postfix.GetParameters().Select(p => (p.Name ?? string.Empty, p.ParameterType)).SequenceEqual(
                new[] { ("__instance", typeof(object)), ("userId", typeof(ulong)), ("seconds", typeof(float)), ("__runOriginal", typeof(bool)) }),
                "Familiarity completion must use Harmony's native-executed postfix gate.");
        List<CodeInstruction> before = PatchProcessor.GetOriginalInstructions(prefix, out _);
        int add = Dispatch(before, AddingId);
        Require(before.Count == add + 4 && before[add + 1].opcode == OpCodes.Ldnull &&
                before[add + 2].opcode == OpCodes.Ceq && before[add + 3].opcode == OpCodes.Ret && add == 6,
                "Only a null admission result may execute native familiarity; boxed false must veto.");
        List<CodeInstruction> after = PatchProcessor.GetOriginalInstructions(postfix, out _);
        int added = Dispatch(after, AddedId);
        Require(added == 8 && after.Count == added + 3 && after[0].opcode == OpCodes.Ldarg_3 &&
                (after[1].opcode == OpCodes.Brfalse || after[1].opcode == OpCodes.Brfalse_S) &&
                after[1].operand is Label label && after[added + 2].labels.Contains(label) &&
                after[added + 1].opcode == OpCodes.Pop && after[added + 2].opcode == OpCodes.Ret,
                "Completion must run exactly once after every normal native exit and never after a veto.");
    }
}
