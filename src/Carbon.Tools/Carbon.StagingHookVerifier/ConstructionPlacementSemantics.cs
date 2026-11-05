using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static class ConstructionPlacementSemantics
{
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static bool Calls(CodeInstruction i, string owner, string name) =>
        (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt) && i.operand is MethodBase method &&
        method.DeclaringType?.FullName == owner && method.Name == name;

    private static int One(IReadOnlyList<CodeInstruction> il, string owner, string name)
    {
        var indices = Enumerable.Range(0, il.Count).Where(i => Calls(il[i], owner, name)).ToArray();
        Require(indices.Length == 1, $"Expected one {owner}.{name} call.");
        return indices[0];
    }

    private static int Target(IReadOnlyList<CodeInstruction> il, object operand)
    {
        Require(operand is Label, "Construction branch label is unresolved.");
        var label = (Label)operand;
        var indices = Enumerable.Range(0, il.Count).Where(i => il[i].labels.Contains(label)).ToArray();
        Require(indices.Length == 1, "Construction branch must resolve one target.");
        return indices[0];
    }

    private static int Local(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Ldloc_0) return 0;
        if (instruction.opcode == OpCodes.Ldloc_1) return 1;
        if (instruction.opcode == OpCodes.Ldloc_2) return 2;
        if (instruction.opcode == OpCodes.Ldloc_3) return 3;
        if (instruction.opcode != OpCodes.Ldloc && instruction.opcode != OpCodes.Ldloc_S) return -1;
        return instruction.operand switch
        {
            LocalBuilder variable => variable.LocalIndex,
            LocalVariableInfo variable => variable.LocalIndex,
            int value => value,
            byte value => value,
            _ => -1
        };
    }

    internal static void Verify(IReadOnlyList<CodeInstruction> il, MethodBase method)
    {
        Require(method.DeclaringType?.FullName == "Planner" && method.Name == "DoPlacement" &&
                method.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "Construction+Target", "Construction" }),
                "Construction hook must retain DoPlacement(Target, Construction).");
        int call = One(il, "Oxide.Core.Interface", "CallHook");
        int start = call - 6;
        Require(start > 0 && il[start].opcode == OpCodes.Ldstr && Equals(il[start].operand, "OnConstructionPlace") &&
                Local(il[start + 1]) == 1 && il[start + 2].opcode == OpCodes.Ldarg_2 &&
                il[start + 3].opcode == OpCodes.Ldarg_1 && il[start + 4].opcode == OpCodes.Box &&
                il[start + 4].operand is Type target && target == method.GetParameters()[0].ParameterType &&
                Local(il[start + 5]) == 0,
                "Construction callback arguments must remain created entity, component, boxed target and owning player.");
        var dispatch = (MethodInfo)il[call].operand;
        Require(dispatch.ReturnType == typeof(object) && dispatch.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                new[] { typeof(string), typeof(object), typeof(object), typeof(object), typeof(object) }),
                "Construction veto must preserve the object-valued callback contract.");
        var locals = method.GetMethodBody()!.LocalVariables;
        Require(locals.Count == 2 && locals[0].LocalType.FullName == "BasePlayer" && locals[1].LocalType.FullName == "BaseEntity",
                "Construction player/entity locals changed.");
        Require(One(il, "HeldEntity", "GetOwnerPlayer") == 1 && il[2].opcode == OpCodes.Stloc_0,
                "Construction callback player must come from the planner owner.");
        int create = One(il, "Construction", "CreateConstruction");
        Require(create + 7 == start && il[create + 1].opcode == OpCodes.Stloc_1 && Local(il[create + 2]) == 1 &&
                Calls(il[create + 3], "UnityEngine.Object", "op_Implicit") &&
                (il[create + 4].opcode == OpCodes.Brtrue || il[create + 4].opcode == OpCodes.Brtrue_S) &&
                Target(il, il[create + 4].operand) == start && il[start - 2].opcode == OpCodes.Ldnull && il[start - 1].opcode == OpCodes.Ret,
                "Only successfully created non-null construction may enter the callback.");
        int continuation = call + 1;
        Require(il[continuation].opcode == OpCodes.Brfalse || il[continuation].opcode == OpCodes.Brfalse_S,
                "Only a null callback result may continue placement (boxed false still vetoes).");
        continuation = Target(il, il[continuation].operand);
        int spawn = One(il, "Planner", "SpawnConstruction");
        Require(spawn == continuation + 6 && il[continuation].opcode == OpCodes.Ldarg_0 &&
                Local(il[continuation + 1]) == 1 && Local(il[continuation + 2]) == 0 &&
                il[continuation + 3].opcode == OpCodes.Ldc_I4_0 && il[continuation + 4].opcode == OpCodes.Conv_I8 &&
                il[continuation + 5].opcode == OpCodes.Ldc_R4 && Equals(il[continuation + 5].operand, 1f) && il[spawn + 1].opcode == OpCodes.Ret,
                "Null continuation must call the untouched native spawn helper with the same entity/player/default inheritance.");
        int valid = One(il, "BaseNetworkableEx", "IsValid");
        int kill = One(il, "BaseNetworkable", "KillMessage");
        int decay = One(il, "DecayEntity", "DoServerDestroy");
        int terminate = One(il, "BaseNetworkable", "TerminateOnServer");
        int destroy = One(il, "BaseNetworkable", "EntityDestroy");
        Require(valid == call + 3 && Local(il[valid - 1]) == 1 && Local(il[kill - 1]) == 1 &&
                Local(il[terminate - 1]) == 1 && Local(il[destroy - 1]) == 1,
                "Cancellation must clean up the exact created entity.");
        Require(il[decay - 1].opcode == OpCodes.Isinst && il[decay - 1].operand is Type decayType && decayType.FullName == "DecayEntity" &&
                Local(il[decay - 2]) == 1 && il[decay - 5].opcode == OpCodes.Isinst && Equals(il[decay - 5].operand, decayType) &&
                Local(il[decay - 6]) == 1 && Calls(il[decay - 4], "UnityEngine.Object", "op_Implicit") &&
                (il[decay - 3].opcode == OpCodes.Brfalse || il[decay - 3].opcode == OpCodes.Brfalse_S) &&
                Target(il, il[decay - 3].operand) == terminate - 1,
                "Unspawned DecayEntity cleanup must recast the exact entity after the native Unity validity check, without synthetic locals.");
        var pending = new Queue<(int Index, bool Killed, bool Terminated, bool Destroyed)>();
        var seen = new HashSet<(int, bool, bool, bool)>();
        pending.Enqueue((call + 2, false, false, false));
        int returned = 0;
        while (pending.TryDequeue(out var state))
        {
            if (!seen.Add(state)) continue;
            int i = state.Index;
            Require(i > call && i < continuation, "Veto path reaches native initialization/spawn or leaves its cleanup region.");
            bool killed = state.Killed || i == kill;
            bool terminated = state.Terminated || i == terminate;
            bool destroyed = state.Destroyed || i == destroy;
            if (il[i].opcode == OpCodes.Ret)
            {
                Require(il[i - 1].opcode == OpCodes.Ldnull && (killed || (terminated && destroyed)),
                        "Every ordinary veto return must clean up the entity and return null.");
                returned++;
                continue;
            }
            if (il[i].opcode.FlowControl == FlowControl.Throw) continue;
            if (il[i].opcode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch)
            {
                pending.Enqueue((Target(il, il[i].operand), killed, terminated, destroyed));
                if (il[i].opcode.FlowControl == FlowControl.Branch) continue;
            }
            pending.Enqueue((i + 1, killed, terminated, destroyed));
        }
        Require(returned > 0, "Construction veto has no verified cleanup/null return.");
    }
}
