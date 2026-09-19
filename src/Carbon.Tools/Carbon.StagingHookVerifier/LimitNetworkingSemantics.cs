using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static class LimitNetworkingSemantics
{
    internal static void Verify(int number, IReadOnlyList<CodeInstruction> before,
        IReadOnlyList<CodeInstruction> after, MethodBase target)
    {
        int limit = Enumerable.Range(0, after.Count).Single(i => Call(after[i], "BaseNetworkable", "get_limitNetworking"));
        switch (number)
        {
            case 1:
            {
                if (target.DeclaringType?.FullName != "Effect+server" || target.Name != "ImpactEffect")
                    Fail("Impact guard target changed.");
                int start = limit - 6, continuation = start + 9;
                if (start < 0 || after[start].opcode != OpCodes.Ldarg_0 ||
                    !Call(after[start + 1], "HitInfo", "get_InitiatorPlayer") ||
                    !Call(after[start + 2], "UnityEngine.Object", "op_Implicit") ||
                    !IsFalse(after[start + 3]) || Target(after, after[start + 3]) != continuation ||
                    after[start + 4].opcode != OpCodes.Ldarg_0 ||
                    !Call(after[start + 5], "HitInfo", "get_InitiatorPlayer") ||
                    !IsFalse(after[start + 7]) || Target(after, after[start + 7]) != continuation ||
                    after[start + 8].opcode != OpCodes.Ret || after[continuation].opcode != OpCodes.Ldarg_0 ||
                    after[continuation + 1].operand is not FieldInfo field || field.Name != "DoHitEffects")
                    Fail("Impact guard must preserve native FX for null/unlimited initiators and return only for limited initiators.");
                Unchanged(before, after, start, 9);
                break;
            }
            case 2:
            {
                if (target.DeclaringType?.FullName != "BaseProjectile" || target.Name != "CLProject")
                    Fail("Projectile guard target changed.");
                int start = limit - 1;
                int effect = Enumerable.Range(start + 3, after.Count - start - 3)
                    .First(i => Call(after[i], "BaseProjectile", "CreateProjectileEffectClientside"));
                int player = Local(after[start]);
                if (player < 0 || target.GetMethodBody()!.LocalVariables[player].LocalType.FullName != "BasePlayer" ||
                    !Call(after[start - 1], "BasePlayer", "NoteFiredProjectile") ||
                    !IsTrue(after[start + 2]) || Target(after, after[start + 2]) != effect + 1 ||
                    after[start + 3].opcode != OpCodes.Ldarg_0)
                    Fail("Projectile guard must follow projectile accounting and skip only clientside projectile FX.");
                Unchanged(before, after, start, 3);
                break;
            }
            case 3:
            {
                if (target.DeclaringType?.FullName != "BasePlayer" || target.Name != "OnAttacked")
                    Fail("Headshot guard target changed.");
                int start = limit - 4, effectStart = start + 6;
                int player = Local(after[start]);
                if (player < 0 || target.GetMethodBody()!.LocalVariables[player].LocalType.FullName != "BasePlayer" ||
                    Local(after[start + 3]) != player || !Call(after[start + 1], "UnityEngine.Object", "op_Implicit") ||
                    !IsFalse(after[start + 2]) || Target(after, after[start + 2]) != effectStart ||
                    !IsTrue(after[start + 5]) || after[effectStart].opcode != OpCodes.Ldstr ||
                    !Equals(after[effectStart].operand, "assets/bundled/prefabs/fx/headshot.prefab"))
                    Fail("Headshot guard must start before the effect argument block and retain null/unlimited FX.");
                int definition = Enumerable.Range(0, start).Last(i => Store(after[i]) == player);
                if (definition < 1 || !Call(after[definition - 1], "HitInfo", "get_InitiatorPlayer"))
                    Fail("Headshot guard must use the native HitInfo.InitiatorPlayer local.");
                int effect = Enumerable.Range(effectStart + 1, after.Count - effectStart - 1)
                    .First(i => Call(after[i], "Effect+server", "Run"));
                if (Target(after, after[start + 5]) != effect + 1 || Local(after[effect + 1]) != player ||
                    !Call(after[effect + 2], "UnityEngine.Object", "op_Implicit"))
                    Fail("Limited initiator must skip exactly Effect.server.Run and retain native headshot statistics/spectator work.");
                if (!Enumerable.Range(0, start).Any(i => after[i].opcode.FlowControl == FlowControl.Branch &&
                        after[i].operand is Label && Target(after, after[i]) == start))
                    Fail("Native flinch branch must enter the limit guard, not bypass it.");
                Unchanged(before, after, start, 6);
                break;
            }
            default: throw new ArgumentOutOfRangeException(nameof(number));
        }
    }
    private static void Unchanged(IReadOnlyList<CodeInstruction> before, IReadOnlyList<CodeInstruction> after, int start, int inserted)
    {
        if (before.Count + inserted != after.Count) Fail("Networking guard must only insert its reviewed instruction block.");
        for (int i = 0; i < before.Count; i++)
        {
            CodeInstruction actual = after[i < start ? i : i + inserted];
            if (actual.opcode != before[i].opcode || !Equals(actual.operand, before[i].operand))
                Fail("Networking guard changed native/base-hook instructions outside its insertion.");
        }
    }
    private static int Local(CodeInstruction i) => LocalIndex(i, false);
    private static int Store(CodeInstruction i) => LocalIndex(i, true);
    private static int LocalIndex(CodeInstruction i, bool store)
    {
        OpCode[] shortCodes = store ? [OpCodes.Stloc_0, OpCodes.Stloc_1, OpCodes.Stloc_2, OpCodes.Stloc_3] : [OpCodes.Ldloc_0, OpCodes.Ldloc_1, OpCodes.Ldloc_2, OpCodes.Ldloc_3];
        int index = Array.IndexOf(shortCodes, i.opcode);
        if (index >= 0) return index;
        if (i.opcode != (store ? OpCodes.Stloc : OpCodes.Ldloc) && i.opcode != (store ? OpCodes.Stloc_S : OpCodes.Ldloc_S)) return -1;
        return i.operand switch { int slot => slot, byte slot => slot, LocalBuilder local => local.LocalIndex, LocalVariableInfo local => local.LocalIndex, _ => -1 };
    }
    private static bool Call(CodeInstruction i, string type, string method) =>
        (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt) && i.operand is MethodInfo m && m.DeclaringType?.FullName == type && m.Name == method;
    private static bool IsTrue(CodeInstruction i) => i.opcode == OpCodes.Brtrue || i.opcode == OpCodes.Brtrue_S;
    private static bool IsFalse(CodeInstruction i) => i.opcode == OpCodes.Brfalse || i.opcode == OpCodes.Brfalse_S;
    private static int Target(IReadOnlyList<CodeInstruction> il, CodeInstruction branch)
    {
        if (branch.operand is not Label label) throw new InvalidOperationException("Expected resolved networking guard branch label.");
        return Enumerable.Range(0, il.Count).Single(i => il[i].labels.Contains(label));
    }
    private static void Fail(string reason) => throw new InvalidOperationException(reason);
}
