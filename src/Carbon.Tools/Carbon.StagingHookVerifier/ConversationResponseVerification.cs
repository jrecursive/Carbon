using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static partial class Program
{
    private static void VerifyConversationResponseSemantics(IReadOnlyList<HookMetadata> hooks,
        List<VerificationFailure> failures, VerificationStats stats)
    {
        const string respond = "OnNpcConversationRespond";
        try
        {
            HookMetadata basic = hooks.Single(hook => hook.HookFullName == respond);
            HookMetadata completed = hooks.Single(hook => hook.HookFullName == "OnNpcConversationResponded");
            Type type = FindType(basic.Target) ?? throw new InvalidOperationException("Conversation hook target is missing.");
            MethodBase target = ResolveTargetMethod(type, basic) ?? throw new InvalidOperationException("Conversation hook signature is missing.");
            if (basic.Target != "NPCTalking" || completed.Target != basic.Target ||
                ResolveTargetMethod(type, completed) != target || !completed.Dependencies.Contains(respond))
                throw new InvalidOperationException("Conversation response hooks must retain the same native target and veto-before-completion dependency.");
            List<CodeInstruction> current = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
            foreach (HookMetadata hook in new[] { basic, completed })
            {
                MethodInfo transpiler = AccessTools.Method(hook.Type.AsType(), "Transpiler")
                    ?? throw new InvalidOperationException("Conversation generated transpiler is missing.");
                current = transpiler.Invoke(null, new object?[] { current, generator, target }) is IEnumerable<CodeInstruction> rewritten
                    ? rewritten.ToList() : throw new InvalidOperationException("Conversation transpiler returned no instructions.");
            }
            ConversationResponseSemantics.Verify(current, target);
            stats.SemanticChecks++;
        }
        catch (Exception error)
        {
            failures.Add(new VerificationFailure(respond, "semantic", Unwrap(error).Message));
        }
    }
}
