using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static partial class Program
{
    private static void VerifyAcquaintanceNetworkingSemantics(
        IReadOnlyList<HookMetadata> hooks, List<VerificationFailure> failures, VerificationStats stats)
    {
        const string name = "LimitNetworkingAcquaintances [patch]";
        try
        {
            HookMetadata patch = hooks.Single(hook => hook.HookFullName == name);
            if (patch.Dependencies.Length != 0)
                throw new InvalidOperationException("Acquaintance guard acquired an unreviewed base dependency.");
            Type type = FindType(patch.Target) ?? throw new InvalidOperationException("Acquaintance target type is missing.");
            MethodBase target = ResolveTargetMethod(type, patch) ?? throw new InvalidOperationException("Acquaintance target method is missing.");
            List<CodeInstruction> native = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
            List<CodeInstruction> snapshot = native.Select(instruction => new CodeInstruction(instruction)).ToList();
            MethodInfo transpiler = AccessTools.Method(patch.Type.AsType(), "Transpiler")
                ?? throw new InvalidOperationException("Acquaintance transpiler is missing.");
            List<CodeInstruction> rewritten = transpiler.Invoke(null, new object?[] { native, generator, target }) is IEnumerable<CodeInstruction> result
                ? result.ToList() : throw new InvalidOperationException("Acquaintance transpiler returned no IL.");
            AcquaintanceNetworkingSemantics.Verify(snapshot, rewritten, target);
            stats.SemanticChecks++;
        }
        catch (Exception error)
        {
            failures.Add(new VerificationFailure(name, "semantic", Unwrap(error).Message));
        }
    }
}
