using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static class AcquaintanceNetworkingSemantics
{
    internal static void Verify(IReadOnlyList<CodeInstruction> before,
        IReadOnlyList<CodeInstruction> after, MethodBase target)
    {
        if (target.DeclaringType?.FullName != "RelationshipManager" || target.Name != "UpdateAcquaintancesFor" ||
            target is not MethodInfo method || method.ReturnType != typeof(void) ||
            !target.GetParameters().Select(parameter => parameter.ParameterType.FullName)
                .SequenceEqual(new[] { "BasePlayer", "System.Single" }))
            Fail("Acquaintance networking guard target changed.");
        int limit = Unique(after, instruction => Call(instruction, "BaseNetworkable", "get_limitNetworking"));
        int start = limit - 1;
        if (start < 0 || after.Count != before.Count + 3)
            Fail("Acquaintance guard must insert exactly three instructions.");
        int peer = Local(after[start], false);
        if (peer < 0 || peer >= target.GetMethodBody()!.LocalVariables.Count ||
            target.GetMethodBody()!.LocalVariables[peer].LocalType.FullName != "BasePlayer" ||
            after[limit].opcode != OpCodes.Callvirt ||
            (after[limit + 1].opcode != OpCodes.Brtrue && after[limit + 1].opcode != OpCodes.Brtrue_S))
            Fail("Acquaintance guard must test the current peer and skip only when networking is limited.");

        int relations = Unique(before, instruction => Call(instruction, "RelationshipManager+PlayerRelationships", "GetRelations"));
        if (start != relations - 4 || Local(before[start], false) < 0 ||
            Local(before[start + 1], false) != peer || before[start + 2].opcode != OpCodes.Ldfld ||
            before[start + 2].operand is not FieldInfo user || user.Name != "userID" ||
            !Call(before[start + 3], null, "op_Implicit"))
            Fail("Acquaintance guard must precede native GetRelations and its complete argument block.");
        int definition = Enumerable.Range(0, start).LastOrDefault(index => Local(before[index], true) == peer, -1);
        if (definition < 2 || !Call(before[definition - 1], null, "get_Current") ||
            before[definition - 1].operand is not MethodInfo current || current.ReturnType.FullName != "BasePlayer" ||
            Local(before[definition - 2], false) < 0 || !IsAddress(before[definition - 2]))
            Fail("Acquaintance guard must use the foreach peer local.");

        int nextCall = Unique(before, instruction => Call(instruction, null, "MoveNext"));
        int next = nextCall - 1;
        if (next <= relations || !IsAddress(before[next]) ||
            Local(before[next], false) != Local(before[definition - 2], false) ||
            (before[nextCall + 1].opcode != OpCodes.Brtrue && before[nextCall + 1].opcode != OpCodes.Brtrue_S) ||
            Target(before, before[nextCall + 1]) != definition - 2 ||
            Target(after, after[limit + 1]) != next + 3)
            Fail("Limited peer must skip into the same native enumerator MoveNext, retaining loop continuation.");
        int seen = Unique(before, instruction => instruction.opcode == OpCodes.Stfld &&
            instruction.operand is FieldInfo field && field.Name == "lastSeenTime");
        if (seen <= relations || seen >= next ||
            !before.Skip(nextCall + 1).Any(instruction => instruction.opcode == OpCodes.Endfinally) ||
            !before.Skip(nextCall + 1).Any(instruction => Call(instruction, "System.IDisposable", "Dispose")) ||
            !before.Skip(nextCall + 1).Any(instruction => Call(instruction, "Facepunch.Pool", "FreeUnmanaged")))
            Fail("Acquaintance skip must bypass lastSeen mutation and retain enumerator disposal and pooled-list release.");
        for (int index = 0; index < before.Count; index++)
        {
            CodeInstruction actual = after[index < start ? index : index + 3];
            if (actual.opcode != before[index].opcode || !Equals(actual.operand, before[index].operand))
                Fail("Acquaintance guard changed native admission, relation work, loop, or cleanup instructions.");
            IReadOnlyList<Label> labels = index == start ? after[start].labels : actual.labels;
            IEnumerable<Label> nativeLabels = index == next && after[limit + 1].operand is Label inserted
                ? labels.Where(label => label != inserted) : labels;
            IReadOnlyList<ExceptionBlock> blocks = index == start ? after[start].blocks : actual.blocks;
            if (!before[index].labels.SequenceEqual(nativeLabels) || !before[index].blocks.SequenceEqual(blocks))
                Fail("Acquaintance guard changed a native branch destination.");
        }
    }

    private static int Unique(IReadOnlyList<CodeInstruction> instructions, Func<CodeInstruction, bool> match)
    {
        int[] found = Enumerable.Range(0, instructions.Count).Where(index => match(instructions[index])).ToArray();
        if (found.Length != 1) Fail("Expected one acquaintance semantic anchor.");
        return found[0];
    }

    private static bool Call(CodeInstruction instruction, string? type, string name) =>
        (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
        instruction.operand is MethodInfo method && method.Name == name &&
        (type == null || method.DeclaringType?.FullName == type);

    private static bool IsAddress(CodeInstruction instruction) =>
        instruction.opcode == OpCodes.Ldloca || instruction.opcode == OpCodes.Ldloca_S;

    private static int Local(CodeInstruction instruction, bool store)
    {
        OpCode[] shortCodes = store ? [OpCodes.Stloc_0, OpCodes.Stloc_1, OpCodes.Stloc_2, OpCodes.Stloc_3]
            : [OpCodes.Ldloc_0, OpCodes.Ldloc_1, OpCodes.Ldloc_2, OpCodes.Ldloc_3];
        int index = Array.IndexOf(shortCodes, instruction.opcode);
        if (index >= 0) return index;
        if (instruction.opcode != (store ? OpCodes.Stloc : OpCodes.Ldloc) &&
            instruction.opcode != (store ? OpCodes.Stloc_S : OpCodes.Ldloc_S) &&
            (store || !IsAddress(instruction))) return -1;
        return instruction.operand switch
        {
            LocalBuilder local => local.LocalIndex, LocalVariableInfo local => local.LocalIndex,
            byte slot => slot, int slot => slot, _ => -1
        };
    }

    private static int Target(IReadOnlyList<CodeInstruction> instructions, CodeInstruction branch)
    {
        if (branch.operand is not Label) Fail("Acquaintance branch lacks a label.");
        Label destination = (Label)branch.operand;
        return Unique(instructions, instruction => instruction.labels.Contains(destination));
    }

    private static void Fail(string message) => throw new InvalidOperationException(message);
}
