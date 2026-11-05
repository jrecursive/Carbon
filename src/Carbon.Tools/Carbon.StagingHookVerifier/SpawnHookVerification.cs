using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static partial class Program
{
    private static void VerifySpawnHookSemantics(IReadOnlyList<HookMetadata> hooks,
        List<VerificationFailure> failures, VerificationStats stats)
    {
        foreach (var expected in new[]
        {
            (Hook: "OnExplosiveThrown", Type: "ThrownWeapon", Method: "DoThrow"),
            (Hook: "OnExplosiveDropped", Type: "ThrownWeapon", Method: "DoDrop"),
            (Hook: "OnEntityBuilt", Type: "Planner", Method: "DoBuild")
        })
        {
            try
            {
                HookMetadata[] found = hooks.Where(h => h.HookFullName == expected.Hook &&
                    h.Target == expected.Type && h.Method == expected.Method).ToArray();
                if (found.Length != 1) throw new InvalidOperationException("Expected exactly one generated hook for " + expected.Hook);
                HookMetadata metadata = found[0];
                Type type = FindType(metadata.Target) ?? throw new InvalidOperationException("Spawn hook target type missing.");
                MethodBase target = ResolveTargetMethod(type, metadata) ?? throw new InvalidOperationException("Spawn hook target method missing.");
                MethodInfo transpiler = AccessTools.Method(metadata.Type.AsType(), "Transpiler") ??
                    throw new InvalidOperationException("Spawn hook transpiler missing.");
                List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
                if (transpiler.Invoke(null, new object[] { original, generator, target }) is not IEnumerable<CodeInstruction> rewritten)
                    throw new InvalidOperationException("Spawn hook transpiler did not return instructions.");
                if (expected.Hook == "OnEntityBuilt") SpawnHookSemantics.Built(rewritten.ToList(), target);
                else SpawnHookSemantics.Explosive(rewritten.ToList(), target, expected.Hook == "OnExplosiveDropped");
                stats.SemanticChecks++;
            }
            catch (Exception ex)
            {
                failures.Add(new VerificationFailure(expected.Hook, "semantic", Unwrap(ex).Message));
            }
        }
    }
}
