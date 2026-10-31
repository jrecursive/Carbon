using System.Reflection;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static partial class Program
{
    private static void VerifyLivestockFamiliaritySemantics(IReadOnlyList<HookMetadata> hooks,
        List<VerificationFailure> failures, VerificationStats stats)
    {
        const string adding = "OnLivestockAnimalFamiliarityAdd";
        try
        {
            HookMetadata basic = hooks.Single(hook => hook.HookFullName == adding);
            HookMetadata completed = hooks.Single(hook => hook.HookFullName == "OnLivestockAnimalFamiliarityAdded");
            Type type = FindType(basic.Target) ?? throw new InvalidOperationException("Livestock familiarity target type is missing.");
            MethodBase target = ResolveTargetMethod(type, basic) ?? throw new InvalidOperationException("Livestock familiarity signature is missing.");
            if (ResolveTargetMethod(type, completed) != target || !completed.Dependencies.Contains(adding))
                throw new InvalidOperationException("Familiarity hooks must retain one target and the admission dependency.");
            MethodInfo prefix = AccessTools.Method(basic.Type.AsType(), "Prefix") ?? throw new InvalidOperationException("Familiarity veto prefix is missing.");
            MethodInfo postfix = AccessTools.Method(completed.Type.AsType(), "Postfix") ?? throw new InvalidOperationException("Familiarity completion postfix is missing.");
            LivestockFamiliaritySemantics.Verify(target, prefix, postfix);
            foreach (string aliasName in new[] { "OnLivestockAnimalFamiliarityAdded [new]", "OnLivestockAnimalFamiliarityAdded [probation]" })
            {
                HookMetadata alias = hooks.Single(hook => hook.HookFullName == aliasName);
                if (ResolveTargetMethod(type, alias) != target || alias.Dependencies.Length != 1 ||
                    !alias.Dependencies.Contains(aliasName.EndsWith("[new]", StringComparison.Ordinal)
                        ? "OnLivestockAnimalFamiliarityAdded" : "OnLivestockAnimalFamiliarityAdded [new]"))
                    throw new InvalidOperationException("Familiarity aliases must retain the single completion chain and native target.");
                MethodInfo identity = AccessTools.Method(alias.Type.AsType(), "Transpiler")
                    ?? throw new InvalidOperationException("Familiarity alias identity transpiler is missing.");
                var native = PatchProcessor.GetOriginalInstructions(target, out _);
                if (!ReferenceEquals(identity.Invoke(null, new object[] { native }), native) ||
                    PatchProcessor.GetOriginalInstructions(identity, out _).Any(instruction => instruction.operand is MethodInfo call && call.Name == "CallStaticHook"))
                    throw new InvalidOperationException("Familiarity aliases must preserve exact native IL and cannot duplicate completion callbacks.");
            }
            stats.SemanticChecks++;
        }
        catch (Exception error)
        {
            failures.Add(new VerificationFailure(adding, "semantic", Unwrap(error).Message));
        }
    }
}
