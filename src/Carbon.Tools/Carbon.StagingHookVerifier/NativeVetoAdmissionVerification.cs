using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static partial class Program
{
    private static void VerifyNativeVetoAdmissionSemantics(IReadOnlyList<HookMetadata> hooks,
        List<VerificationFailure> failures, VerificationStats stats)
    {
        foreach (string name in new[] { "CanHackCrate", "OnFrankensteinPetSleep [FrankensteinTable]" })
        {
            try
            {
                HookMetadata hook = hooks.Single(candidate => candidate.HookFullName == name);
                Type type = FindType(hook.Target) ?? throw new InvalidOperationException("Native veto target type is missing.");
                MethodBase target = ResolveTargetMethod(type, hook) ?? throw new InvalidOperationException("Native veto target signature is missing.");
                MethodInfo transpiler = AccessTools.Method(hook.Type.AsType(), "Transpiler")
                    ?? throw new InvalidOperationException("Native veto generated transpiler is missing.");
                List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
                object? result = transpiler.Invoke(null, new object?[] { original, generator, target });
                NativeVetoAdmissionSemantics.Verify(result is IEnumerable<CodeInstruction> rewritten ? rewritten.ToList() :
                    throw new InvalidOperationException("Native veto transpiler returned no instructions."), target);
                stats.SemanticChecks++;
            }
            catch (Exception error)
            {
                failures.Add(new VerificationFailure(name, "semantic", Unwrap(error).Message));
            }
        }
    }
}
