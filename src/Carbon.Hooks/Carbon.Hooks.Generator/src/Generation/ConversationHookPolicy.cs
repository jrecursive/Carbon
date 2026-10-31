using System.Text;
using Carbon.Projects.Oxide;
using Carbon.Utility;
using static Carbon.Projects.Oxide.Oxide;

namespace Carbon.Generation;

internal static partial class HookPolicies
{
    // Rust 26341433 split response selection and the next/ended transition into
    // ConversationHost. Keep the public four-argument hook ABI at the NPC owner.
    private static bool TryGenerateConversationResponsePolicy(StringBuilder body, HookDef.Data hook)
    {
        if (hook.TypeName != "NPCTalking" || hook.Signature.Name != "HandleResponse" ||
            hook.Name != hook.HookName || hook.Name is not ("OnNpcConversationRespond" or "OnNpcConversationResponded"))
            return false;

        Helper.Parameters.Add(("self", Tools.TypeByNameEx("NPCTalking")!));
        Helper.Parameters.Add(("player", Tools.TypeByNameEx("BasePlayer")!));
        Helper.Parameters.Add(("conversation", Tools.TypeByNameEx("ConversationData")!));
        Helper.Parameters.Add(("response", Tools.TypeByNameEx("ConversationData+ResponseNode")!));
        bool completed = hook.Name == "OnNpcConversationResponded";
        Helper.ReturnDiscarded = completed;
        if (!completed) Helper.ReturnType = typeof(void);

        body.AppendLine("""
public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method) {
    if (method.Module.ModuleVersionId.ToString() != "a5e8e418-dabf-43a5-8e0d-9fd08f1715b1")
        throw new InvalidOperationException("Unreviewed native NPC conversation module.");
    VerifyNative(method);
    var il = new List<CodeInstruction>(instructions);
""");
        if (!completed)
        {
            body.AppendLine("""
    int[] matches = Enumerable.Range(0, il.Count).Where(i => il[i].operand is MethodInfo call &&
        call.DeclaringType == typeof(ConversationHost) && call.Name == "TryGetResponseResult").ToArray();
    if (matches.Length != 1 || matches[0] < 6) throw new InvalidOperationException("Native response-result boundary changed.");
    int at = matches[0] - 6;
    if (il[at].opcode != OpCodes.Ldarg_0 || il[at + 1].operand is not MethodInfo host || host.Name != "get_ConversationHost" ||
        il[at + 2].opcode != OpCodes.Ldarg_1 || il[at + 3].opcode != OpCodes.Ldloc_1 ||
        il[at + 4].opcode != OpCodes.Ldarg_3 || il[at + 5].opcode != OpCodes.Ldloca_S)
        throw new InvalidOperationException("Native response-result arguments changed.");
    LocalBuilder selected = generator.DeclareLocal(typeof(ConversationData.ResponseNode));
    Label next = generator.DefineLabel(); il[at].labels.Add(next);
    var added = new List<CodeInstruction> {
        new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1),
        new CodeInstruction(OpCodes.Ldloc_0), new CodeInstruction(OpCodes.Ldloc_1),
        new CodeInstruction(OpCodes.Ldarg_3), new CodeInstruction(OpCodes.Ldloca, selected),
        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(REPLACE_CLASS), nameof(BeforeResponse))),
        new CodeInstruction(OpCodes.Brtrue, next), new CodeInstruction(OpCodes.Ret)
    };
    // Native mission-preview branches to the admission boundary must run the hook.
    added[0].MoveLabelsFrom(il[at]).MoveBlocksFrom(il[at]);
    added[0].labels.Remove(next); il[at].labels.Add(next);
    il.InsertRange(at, added); return il;
}
public static bool BeforeResponse(NPCTalking self, BasePlayer player, ConversationData conversation,
    ConversationData.AbstractSpeechNodeData speech, int responseIndex, out ConversationData.ResponseNode selected) {
    selected = null!;
    if (responseIndex < 0 || responseIndex >= speech.responses.Length) return true;
    var response = speech.responses[responseIndex];
    selected = response;
    return response == null || HookCaller.CallStaticHook(REPLACE_IDu, self, player, conversation, response) == null;
}
""".Replace("REPLACE_ID", HookStringPool.GetOrAdd(hook.HookName).ToString()));
        }
        else
        {
            body.AppendLine("""
    int[] vetoes = Enumerable.Range(0, il.Count).Where(i => il[i].operand is MethodInfo call &&
        call.Name == "BeforeResponse" && call.GetParameters().Length == 6).ToArray();
    // Harmony rebuilds after each unpatch. Once the prerequisite has gone,
    // leave the native method intact while the dependent hook is detached.
    if (vetoes.Length == 0) return il;
    if (vetoes.Length != 1 || vetoes[0] < 1 || il[vetoes[0] - 1].opcode != OpCodes.Ldloca ||
        il[vetoes[0] - 1].operand is not LocalBuilder selected || selected.LocalType != typeof(ConversationData.ResponseNode))
        throw new InvalidOperationException("Conversation completion requires the admitted response-veto dependency.");
    int[] matches = Enumerable.Range(0, il.Count).Where(i => il[i].operand is MethodInfo call &&
        call.DeclaringType == typeof(ConversationHost) && call.Name == "ShowNextSpeechNode").ToArray();
    if (matches.Length != 1) throw new InvalidOperationException("Native conversation transition boundary changed.");
    int at = matches[0];
    var added = new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldloc, selected),
        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(REPLACE_CLASS), nameof(CompleteResponse))) };
    added[0].MoveLabelsFrom(il[at]).MoveBlocksFrom(il[at]);
    il.RemoveAt(at); il.InsertRange(at, added); return il;
}
public static void CompleteResponse(ConversationHost host, BasePlayer player, ConversationData conversation,
    string resultingNode, NPCTalking self, ConversationData.ResponseNode selected) {
    // Exact reviewed ShowNextSpeechNode behavior: ended calls never notify, and
    // successful transitions still use native ForceSpeechNode to build/send state.
    conversation.GetFirstSpeechNodeFrom(player, self, resultingNode, out int nodeIndex);
    if (nodeIndex == -1) { self.OnConversationEnded(player); return; }
    host.ForceSpeechNode(player, conversation, nodeIndex);
    HookCaller.CallStaticHook(REPLACE_IDu, self, player, conversation, selected);
}
""".Replace("REPLACE_ID", HookStringPool.GetOrAdd(hook.HookName).ToString()));
        }
        body.AppendLine("""
private static void VerifyNative(MethodBase response) {
    var pins = new Dictionary<MethodBase, string> {
        [response] = "06a3acc04d8d6a2e788dcf26ade05f2e487338ab60e3a05517cd44f9fd31a856",
        [AccessTools.Method(typeof(ConversationHost), "CanDriveConversation")] = "fff862ae7526c9197fd3f03ab99c6bdd1f6fb4874194391551357cdbd593d428",
        [AccessTools.Method(typeof(ConversationHost), "TryGetShownSpeechNode")] = "6d529f8694c2a82fac3b6fc119826c667b045ec6b97f9ab0486df8aec69c1ce4",
        [AccessTools.Method(typeof(ConversationHost), "TryGetResponseResult")] = "cf29e13702fabdfdd834bd676b2a755ef14a63ef421eb082fe0e09d52e25d533",
        [AccessTools.Method(typeof(ConversationHost), "ShowNextSpeechNode")] = "2285cbffa388dd8d4c6db615cd7d3e00497d409d9f0aa502c61e4f0db6db3307"
    };
    foreach (var pin in pins) {
        var il = PatchProcessor.GetOriginalInstructions(pin.Key, out _);
        string Operand(object value) => value switch {
            MethodBase call => call.DeclaringType!.FullName + ":" + call.Name,
            FieldInfo field => field.DeclaringType!.FullName + ":" + field.Name,
            Label label => "@" + il.FindIndex(i => i.labels.Contains(label)),
            LocalBuilder local => "V" + local.LocalIndex,
            Type type => type.FullName!, null => "", _ => value.ToString()!
        };
        MethodBody native = pin.Key.GetMethodBody()!;
        string shape = native.MaxStackSize + "/" + native.InitLocals + "/" +
            string.Join(",", native.LocalVariables.Select(local => local.LocalType.FullName)) + "/" +
            native.ExceptionHandlingClauses.Count + "\n" + string.Join("\n", il.Select(i => i.opcode.Name + "|" + Operand(i.operand)));
        using var hash = System.Security.Cryptography.SHA256.Create();
        string actual = BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(shape))).Replace("-", "").ToLowerInvariant();
        if (actual != pin.Value) throw new InvalidOperationException("Unreviewed native conversation helper: " + pin.Key.Name);
    }
}
""");
        // Resolve our own generated class without duplicating generator identifier rules.
        string emitted = body.ToString();
        int declaration = emitted.LastIndexOf("public class ", StringComparison.Ordinal);
        int suffix = emitted.IndexOf(" : API.Hooks.Patch", declaration, StringComparison.Ordinal);
        string className = emitted[(declaration + "public class ".Length)..suffix];
        body.Replace("REPLACE_CLASS", className);
        CloseGeneratedHookBody(body);
        return true;
    }
}
