using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static partial class Program
{
    private static void VerifyCollectiblePickupSemantics(IReadOnlyList<HookMetadata> hooks,
        List<VerificationFailure> failures, VerificationStats stats)
    {
        try
        {
            HookMetadata before = hooks.Single(hook => hook.HookFullName == "OnCollectiblePickup");
            HookMetadata after = hooks.Single(hook => hook.HookFullName == "OnCollectiblePickedup");
            if (before.Target != "CollectibleEntity" || after.Target != before.Target ||
                before.Method != "DoPickup" || after.Method != before.Method ||
                !after.Dependencies.Contains(before.HookFullName))
                throw new InvalidOperationException("Collectible notifications must retain their exact target and pickup-veto dependency.");
            Type type = FindType(before.Target) ?? throw new InvalidOperationException("Collectible target type is missing.");
            MethodBase target = ResolveTargetMethod(type, before) ?? throw new InvalidOperationException("Collectible pickup signature is missing.");
            if (ResolveTargetMethod(type, after) != target)
                throw new InvalidOperationException("Collectible hooks resolve different native methods.");
            List<CodeInstruction> current = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
            foreach (HookMetadata hook in new[] { before, after })
            {
                MethodInfo transpiler = AccessTools.Method(hook.Type.AsType(), "Transpiler")
                    ?? throw new InvalidOperationException("Collectible generated transpiler is missing.");
                current = transpiler.Invoke(null, new object?[] { current, generator, target }) is IEnumerable<CodeInstruction> rewritten
                    ? rewritten.ToList() : throw new InvalidOperationException("Collectible transpiler returned no instruction stream.");
            }
            CollectiblePickupSemantics.Verify(current, target);
            stats.SemanticChecks++;
        }
        catch (Exception error)
        {
            failures.Add(new VerificationFailure("OnCollectiblePickedup", "semantic", Unwrap(error).Message));
        }
    }
}
