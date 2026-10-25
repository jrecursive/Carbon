using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static class CollectiblePickupSemantics
{
    internal const uint BeforeId = unchecked((uint)-1004023405);
    internal const uint AfterId = 93268119u;
    private const int BeforeNative = 4, BeforeLength = 9, AfterNative = 119, AfterLength = 6;

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Verify(IReadOnlyList<CodeInstruction> il, MethodBase target)
    {
        Require(target.DeclaringType?.FullName == "CollectibleEntity" && target.Name == "DoPickup" &&
                target is MethodInfo method && method.ReturnType == typeof(void) &&
                target.GetParameters().Select(parameter => parameter.ParameterType.FullName).SequenceEqual(
                    new[] { "BasePlayer", "System.Boolean" }),
                "Collectible pickup must retain its exact native receiver/eat signature.");
        List<CodeInstruction> native = PatchProcessor.GetOriginalInstructions(target, out _);
        MethodBody body = target.GetMethodBody()!;
        Require(native.Count == 190 && body.ExceptionHandlingClauses.Count == 0 && body.LocalVariables.Count == 11 &&
                body.LocalVariables[8].LocalType.FullName == "Item" &&
                body.LocalVariables[9].LocalType.FullName == "ItemModConsume",
                "Collectible native control flow or Item local changed; review the successful-delivery boundary.");

        // These guards distinguish a real receiver/item delivery from failed
        // creation, consumed food and the receiver-null fallback drop. The loop
        // returns to creation for each configured output; Kill remains after it.
        Require(native[1].opcode == OpCodes.Ldfld && native[1].operand is FieldInfo items && items.Name == "itemList" &&
                native[2].opcode == OpCodes.Brtrue_S && Target(native, native[2].operand) == BeforeNative &&
                native[3].opcode == OpCodes.Ret && Local(native[75]) == 8 &&
                native[77].opcode == OpCodes.Brfalse && Target(native, native[77].operand) == 139 &&
                native[83].opcode == OpCodes.Ldarg_2 && native[84].opcode == OpCodes.Brfalse_S &&
                Target(native, native[84].operand) == 107 && Call(native[105], "DoAction") &&
                native[106].opcode == OpCodes.Br_S && Target(native, native[106].operand) == 139 &&
                native[107].opcode == OpCodes.Ldarg_1 && Call(native[108], "op_Implicit") &&
                native[109].opcode == OpCodes.Brfalse_S && Target(native, native[109].operand) == 125 &&
                Call(native[118], "OnGatherItem") && native[119].opcode == OpCodes.Ldarg_1 &&
                Local(native[120]) == 8 && Call(native[123], "GiveItem") &&
                native[124].opcode == OpCodes.Br_S && Target(native, native[124].operand) == 139 &&
                Call(native[137], "Drop") && native[147].opcode == OpCodes.Blt &&
                Target(native, native[147].operand) == 32 && Call(native[188], "Kill"),
                "Collectible successful receiver, item-null, eat, drop, loop or kill paths changed.");

        int[] dispatches = Enumerable.Range(0, il.Count).Where(index => Call(il[index], "CallStaticHook")).ToArray();
        int before = BeforeNative, after = AfterNative + BeforeLength;
        Require(il.Count == native.Count + BeforeLength + AfterLength && dispatches.SequenceEqual(new[] { before + 5, after + 4 }),
                "Collectible hooks must compose once at entry admission and successful receiver delivery.");
        Require(il[before].opcode == OpCodes.Ldc_I4 && Equals(il[before].operand, unchecked((int)BeforeId)) &&
                il[before + 1].opcode == OpCodes.Ldarg_0 && il[before + 2].opcode == OpCodes.Ldarg_1 &&
                il[before + 3].opcode == OpCodes.Ldarg_2 && il[before + 4].opcode == OpCodes.Box &&
                Equals(il[before + 4].operand, typeof(bool)) && il[before + 6].opcode == OpCodes.Ldnull &&
                il[before + 7].opcode == OpCodes.Beq_S && Target(il, il[before + 7].operand) == before + BeforeLength &&
                il[before + 8].opcode == OpCodes.Ret,
                "Collectible pickup veto must retain exact entity/player/eat identity and null-only continuation.");
        Require(il[after].opcode == OpCodes.Ldc_I4 && Equals(il[after].operand, unchecked((int)AfterId)) &&
                il[after + 1].opcode == OpCodes.Ldarg_0 && il[after + 2].opcode == OpCodes.Ldarg_1 &&
                il[after + 3].opcode == OpCodes.Ldloc_S && Local(il[after + 3]) == 8 &&
                il[after + 5].opcode == OpCodes.Pop,
                "Collectible completion must pass entity/player/created Item and discard its return before native GiveItem.");
        foreach (int index in dispatches)
        {
            MethodInfo dispatch = (MethodInfo)il[index].operand;
            Require(dispatch.ReturnType == typeof(object) && dispatch.GetParameters().Select(parameter => parameter.ParameterType)
                    .SequenceEqual(new[] { typeof(uint), typeof(object), typeof(object), typeof(object) }),
                    "Collectible callback ABI changed.");
        }

        // Every native effect and branch survives composition. A branch into
        // an insertion boundary must enter its callback rather than bypass it.
        for (int index = 0; index < native.Count; index++)
        {
            CodeInstruction original = native[index], rewritten = il[MapInstruction(index)];
            Require(original.opcode == rewritten.opcode, "Collectible hook changed a native opcode.");
            if (original.operand is Label label)
                Require(Target(il, rewritten.operand) == MapTarget(Target(native, label)),
                    "Collectible hook changed native item/receiver/consume/loop control flow.");
            else if (original.operand is LocalBuilder local)
                Require(rewritten.operand is LocalBuilder changed && changed.LocalIndex == local.LocalIndex,
                    "Collectible hook changed a native local operand.");
            else
                Require(Equals(original.operand, rewritten.operand), "Collectible hook changed a native operand or effect.");
        }
    }

    private static int MapInstruction(int index) => index + (index >= BeforeNative ? BeforeLength : 0) +
        (index >= AfterNative ? AfterLength : 0);
    private static int MapTarget(int index) => index + (index > BeforeNative ? BeforeLength : 0) +
        (index > AfterNative ? AfterLength : 0);

    private static bool Call(CodeInstruction instruction, string name) =>
        (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
        instruction.operand is MethodInfo method && method.Name == name;

    private static int Local(CodeInstruction instruction) => instruction.operand switch
    {
        LocalBuilder local => local.LocalIndex,
        byte value => value,
        sbyte value => value,
        int value => value,
        _ => -1
    };

    private static int Target(IReadOnlyList<CodeInstruction> il, object operand)
    {
        if (operand is not Label label) throw new InvalidOperationException("Collectible branch lacks a resolved label.");
        int[] matches = Enumerable.Range(0, il.Count).Where(index => il[index].labels.Contains(label)).ToArray();
        Require(matches.Length == 1, "Collectible native branch must resolve to exactly one instruction.");
        return matches[0];
    }
}
