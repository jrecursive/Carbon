using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static partial class Program
{
    private static void VerifyConstructionPlacementSemantics(IReadOnlyList<HookMetadata> hooks,
        List<VerificationFailure> failures, VerificationStats stats)
    {
        try
        {
            HookMetadata metadata = hooks.Single(h => h.HookFullName == "OnConstructionPlace");
            Type type = FindType(metadata.Target) ?? throw new InvalidOperationException("Construction hook target type missing.");
            MethodBase target = ResolveTargetMethod(type, metadata) ?? throw new InvalidOperationException("Construction hook target missing.");
            MethodInfo transpiler = AccessTools.Method(metadata.Type.AsType(), "Transpiler") ?? throw new InvalidOperationException("Construction hook transpiler missing.");
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
            if (transpiler.Invoke(null, new object[] { original, generator, target }) is not IEnumerable<CodeInstruction> rewritten)
                throw new InvalidOperationException("Construction hook transpiler did not return instructions.");
            ConstructionPlacementSemantics.Verify(rewritten.ToList(), target);
            stats.SemanticChecks++;
        }
        catch (Exception ex)
        {
            failures.Add(new VerificationFailure("OnConstructionPlace", "semantic", Unwrap(ex).Message));
        }
    }
}
