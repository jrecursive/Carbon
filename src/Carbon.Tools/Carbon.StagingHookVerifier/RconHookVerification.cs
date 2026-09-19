using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static partial class Program
{
    private static void VerifyWebRconAdmissionSemantics(
        IReadOnlyList<HookMetadata> hooks, List<VerificationFailure> failures, VerificationStats stats)
    {
        const string baseName = "OnRconConnection [web]";
        const string patchName = "OnRconConnection [web, patch]";
        try
        {
            HookMetadata basic = hooks.Single(h => h.HookFullName == baseName);
            HookMetadata patch = hooks.Single(h => h.HookFullName == patchName);
            if (basic.Target != "Facepunch.Rcon.Listener" || patch.Target != basic.Target ||
                basic.Method != "OnConnection" || patch.Method != basic.Method || !patch.Dependencies.Contains(baseName))
                throw new InvalidOperationException("Both web RCON hooks must target Listener.OnConnection and retain their dependency order.");
            Type targetType = FindType(basic.Target) ?? throw new InvalidOperationException("RCON listener type is missing.");
            MethodBase target = ResolveTargetMethod(targetType, basic) ?? throw new InvalidOperationException("RCON admission signature is missing.");
            if (ResolveTargetMethod(targetType, patch) != target)
                throw new InvalidOperationException("Web RCON hooks resolve different admission methods.");
            List<CodeInstruction> current = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
            foreach (HookMetadata hook in new[] { basic, patch })
            {
                MethodInfo transpiler = AccessTools.Method(hook.Type.AsType(), "Transpiler")
                    ?? throw new InvalidOperationException("Web RCON generated transpiler is missing.");
                object? result = transpiler.Invoke(null, new object?[] { current, generator, target });
                current = result is IEnumerable<CodeInstruction> rewritten ? rewritten.ToList()
                    : throw new InvalidOperationException("Web RCON transpiler returned no instruction stream.");
            }
            RconHookSemantics.Verify(current, target);
            stats.SemanticChecks++;
        }
        catch (Exception error)
        {
            failures.Add(new VerificationFailure(baseName, "semantic", Unwrap(error).Message));
        }
    }
}
