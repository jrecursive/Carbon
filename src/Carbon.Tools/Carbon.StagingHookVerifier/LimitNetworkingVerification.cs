using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static partial class Program
{
    private static void VerifyLimitNetworkingSemantics(
        IReadOnlyList<HookMetadata> hooks, List<VerificationFailure> failures, VerificationStats stats)
    {
        string[] bases = ["OnImpactEffectCreate", "OnWeaponFired", "IOnBasePlayerAttacked"];
        for (int number = 1; number <= 3; number++)
        {
            string name = $"LimitNetworkingNoEffect [patch {number}]";
            try
            {
                HookMetadata basic = hooks.Single(h => h.HookFullName == bases[number - 1]);
                HookMetadata[] matches = hooks.Where(h => h.HookFullName == name).ToArray();
                if (matches.Length == 0)
                {
                    Type nativeType = FindType(basic.Target) ?? throw new InvalidOperationException("Native networking FX target type is missing.");
                    MethodBase nativeTarget = ResolveTargetMethod(nativeType, basic) ?? throw new InvalidOperationException("Native networking FX target is missing.");
                    LimitNetworkingSemantics.VerifyNative(number, PatchProcessor.GetOriginalInstructions(nativeTarget, out _), nativeTarget);
                    stats.SemanticChecks++;
                    continue;
                }
                HookMetadata patch = matches.Single();
                if (!patch.Dependencies.Contains(basic.HookFullName))
                    throw new InvalidOperationException("Networking FX patch lost its declared base hook dependency.");
                Type type = FindType(patch.Target) ?? throw new InvalidOperationException("Networking FX target type is missing.");
                MethodBase target = ResolveTargetMethod(type, patch) ?? throw new InvalidOperationException("Networking FX target method is missing.");
                if (ResolveTargetMethod(type, basic) != target)
                    throw new InvalidOperationException("Networking FX patch/base resolve different methods.");
                List<CodeInstruction> native = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
                List<CodeInstruction> Apply(HookMetadata hook, List<CodeInstruction> source)
                {
                    MethodInfo transpiler = AccessTools.Method(hook.Type.AsType(), "Transpiler")
                        ?? throw new InvalidOperationException("Networking FX transpiler is missing.");
                    return transpiler.Invoke(null, new object?[] { source, generator, target }) is IEnumerable<CodeInstruction> result
                        ? result.ToList() : throw new InvalidOperationException("Networking FX transpiler returned no IL.");
                }
                List<CodeInstruction> basicIl = Apply(basic, native);
                List<CodeInstruction> snapshot = basicIl.Select(instruction => new CodeInstruction(instruction)).ToList();
                LimitNetworkingSemantics.Verify(number, snapshot, Apply(patch, basicIl), target);
                stats.SemanticChecks++;
            }
            catch (Exception error)
            {
                failures.Add(new VerificationFailure(name, "semantic", Unwrap(error).Message));
            }
        }
    }
}
