using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static class ConversationResponseSemantics
{
    internal const int RespondId = 1580074841;
    internal const int RespondedId = unchecked((int)2723808568u);
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static bool Calls(CodeInstruction instruction, string owner, string name) =>
        instruction.operand is MethodBase method && method.DeclaringType?.FullName == owner && method.Name == name;
    private static int One(IReadOnlyList<CodeInstruction> il, string name) =>
        Enumerable.Range(0, il.Count).Single(i => il[i].operand is MethodInfo method && method.Name == name);
    private static int Target(IReadOnlyList<CodeInstruction> il, object operand)
    {
        Require(operand is Label, "Conversation branch label is unresolved.");
        return Enumerable.Range(0, il.Count).Single(i => il[i].labels.Contains((Label)operand));
    }
    private static string Shape(MethodBase method)
    {
        List<CodeInstruction> il = PatchProcessor.GetOriginalInstructions(method, out _);
        string Operand(object? value) => value switch
        {
            MethodBase call => call.DeclaringType!.FullName + ":" + call.Name,
            FieldInfo field => field.DeclaringType!.FullName + ":" + field.Name,
            Label label => "@" + il.FindIndex(i => i.labels.Contains(label)),
            LocalBuilder local => "V" + local.LocalIndex,
            Type type => type.FullName!, null => "", _ => value.ToString()!
        };
        MethodBody body = method.GetMethodBody()!;
        string value = body.MaxStackSize + "/" + body.InitLocals + "/" +
            string.Join(",", body.LocalVariables.Select(local => local.LocalType.FullName)) + "/" +
            body.ExceptionHandlingClauses.Count + "\n" + string.Join("\n", il.Select(i => i.opcode.Name + "|" + Operand(i.operand)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
    private static int Dispatch(IReadOnlyList<CodeInstruction> il, int id)
    {
        int index = One(il, "CallStaticHook");
        Require(il[index - 5].opcode == OpCodes.Ldc_I4 && Equals(il[index - 5].operand, id), "Conversation callback identity changed.");
        MethodInfo dispatch = (MethodInfo)il[index].operand;
        Require(dispatch.ReturnType == typeof(object) && dispatch.GetParameters().Select(p => p.ParameterType).SequenceEqual(
            new[] { typeof(uint), typeof(object), typeof(object), typeof(object), typeof(object) }), "Conversation callback ABI changed.");
        return index;
    }
    internal static void Verify(IReadOnlyList<CodeInstruction> il, MethodBase method,
        Func<MethodBase, List<CodeInstruction>>? read = null)
    {
        read ??= method => PatchProcessor.GetOriginalInstructions(method, out _);
        Require(method.DeclaringType?.FullName == "NPCTalking" && method.Name == "HandleResponse" &&
            method.Module.ModuleVersionId.ToString() == "a5e8e418-dabf-43a5-8e0d-9fd08f1715b1" &&
            method.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(new[] {
                "BasePlayer", "System.Int32", "System.Int32", "System.UInt32", "System.UInt32", "NetworkableId" }),
            "Conversation hooks must retain the reviewed NPC typed response entrypoint.");
        Assembly game = method.DeclaringType!.Assembly;
        Type host = game.GetType("ConversationHost")!;
        // Resolved IL includes branch targets, local layout, operands, stack and EH;
        // both raw and Carbon-publicized references must prove the same contract.
        var pins = new Dictionary<MethodBase, string> {
            [method] = "06a3acc04d8d6a2e788dcf26ade05f2e487338ab60e3a05517cd44f9fd31a856",
            [host.GetMethod("CanDriveConversation")!] = "fff862ae7526c9197fd3f03ab99c6bdd1f6fb4874194391551357cdbd593d428",
            [host.GetMethod("TryGetShownSpeechNode")!] = "6d529f8694c2a82fac3b6fc119826c667b045ec6b97f9ab0486df8aec69c1ce4",
            [host.GetMethod("TryGetResponseResult")!] = "cf29e13702fabdfdd834bd676b2a755ef14a63ef421eb082fe0e09d52e25d533",
            [host.GetMethod("ShowNextSpeechNode")!] = "2285cbffa388dd8d4c6db615cd7d3e00497d409d9f0aa502c61e4f0db6db3307"
        };
        foreach (var pin in pins) Require(Shape(pin.Key) == pin.Value, "Reviewed native conversation contract changed: " + pin.Key.Name);
        MethodInfo rpc = game.GetType("BasePlayer")!.GetMethod("Server_ConversationResponse", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        Require(rpc.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "BaseEntity+RPC_Server+FromOwner") &&
            read(rpc).Any(i => Calls(i, "IConversationProvider", "HandleResponse")), "Owning player RPC admission/routing changed.");
        List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(method, out _);
        Require(original.Count == 166 && il.Count == 177, "Conversation policy must insert only the reviewed veto and transition wrapper.");
        int before = One(il, "BeforeResponse"), completed = One(il, "CompleteResponse");
        Require(before == 156 && completed == 175 && il[before - 6].opcode == OpCodes.Ldarg_0 &&
            il[before - 5].opcode == OpCodes.Ldarg_1 && il[before - 4].opcode == OpCodes.Ldloc_0 &&
            il[before - 3].opcode == OpCodes.Ldloc_1 && il[before - 2].opcode == OpCodes.Ldarg_3 &&
            il[before - 1].opcode == OpCodes.Ldloca && il[before - 1].operand is LocalBuilder selected &&
            selected.LocalType.FullName == "ConversationData+ResponseNode" && il[before + 1].opcode == OpCodes.Brtrue &&
            Target(il, il[before + 1].operand) == before + 3 && il[before + 2].opcode == OpCodes.Ret,
            "Response veto must retain admitted NPC/player/conversation/response selection and return on cancellation.");
        Require(il[completed - 2].opcode == OpCodes.Ldarg_0 && il[completed - 1].opcode == OpCodes.Ldloc &&
            Equals(il[completed - 1].operand, il[before - 1].operand), "Completion must retain the original selected response local.");
        // All existing admission, mission, visibility, native helper and return IL
        // remains byte-for-byte in meaning; only the final transition call changes.
        int Position(int old) => old < 150 ? old : old < 164 ? old + 9 : old + 11;
        for (int n = 0; n < original.Count; n++)
        {
            if (n == 164) continue;
            CodeInstruction prior = original[n], current = il[Position(n)];
            Require(prior.opcode == current.opcode, "Conversation policy changed a native opcode at " + n);
            if (prior.operand is Label)
                Require(Target(il, current.operand) == (Target(original, prior.operand) == 150 ? 150 : Position(Target(original, prior.operand))),
                    "Conversation policy changed native admission/control flow at " + n);
            else if (prior.operand is LocalBuilder local)
                Require(current.operand is LocalBuilder other && other.LocalIndex == local.LocalIndex && other.LocalType == local.LocalType,
                    "Conversation policy changed a native local at " + n);
            else Require(Equals(prior.operand, current.operand), "Conversation policy changed a native operand at " + n);
        }
        MethodInfo beforeMethod = (MethodInfo)il[before].operand, completeMethod = (MethodInfo)il[completed].operand;
        List<CodeInstruction> veto = read(beforeMethod), completion = read(completeMethod);
        int vetoDispatch = Dispatch(veto, RespondId), completeDispatch = Dispatch(completion, RespondedId);
        Require(veto.Count(i => i.opcode == OpCodes.Ldelem_Ref) == 1 && veto.FindIndex(i => i.opcode == OpCodes.Ldelem_Ref) < vetoDispatch &&
            veto[vetoDispatch + 1].opcode == OpCodes.Ldnull && veto[vetoDispatch + 2].opcode == OpCodes.Ceq &&
            veto[vetoDispatch + 3].opcode == OpCodes.Ret && veto[vetoDispatch - 4].opcode == OpCodes.Ldarg_0 &&
            veto[vetoDispatch - 3].opcode == OpCodes.Ldarg_1 && veto[vetoDispatch - 2].opcode == OpCodes.Ldarg_2,
            "Selected non-null response hook must preserve arguments and null-only continuation before native effects.");
        int find = One(completion, "GetFirstSpeechNodeFrom"), end = One(completion, "OnConversationEnded"), next = One(completion, "ForceSpeechNode");
        Require(find < end && end < next && next < completeDispatch && completion[end + 1].opcode == OpCodes.Ret &&
            completion[completeDispatch + 1].opcode == OpCodes.Pop && completion[completeDispatch + 2].opcode == OpCodes.Ret &&
            completion[completeDispatch - 4].opcode == OpCodes.Ldarg_S && Convert.ToInt32(completion[completeDispatch - 4].operand) == 4 &&
            completion[completeDispatch - 3].opcode == OpCodes.Ldarg_1 && completion[completeDispatch - 2].opcode == OpCodes.Ldarg_2 &&
            completion[completeDispatch - 1].opcode == OpCodes.Ldarg_S && Convert.ToInt32(completion[completeDispatch - 1].operand) == 5,
            "Completion must follow native ForceSpeechNode with original arguments; ended conversations must return without notification.");
    }
}
