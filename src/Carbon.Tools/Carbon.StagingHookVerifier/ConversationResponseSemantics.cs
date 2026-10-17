using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static class ConversationResponseSemantics
{
    internal const int RespondId = 1580074841;
    internal const int RespondedId = unchecked((int)2723808568u);

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static bool Calls(CodeInstruction instruction, string owner, string name) =>
        (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
        instruction.operand is MethodBase method && method.DeclaringType?.FullName == owner && method.Name == name;

    private static int One(IReadOnlyList<CodeInstruction> il, string owner, string name)
    {
        int[] found = Enumerable.Range(0, il.Count).Where(i => Calls(il[i], owner, name)).ToArray();
        Require(found.Length == 1, $"Expected exactly one {owner}.{name} call.");
        return found[0];
    }

    private static int Target(IReadOnlyList<CodeInstruction> il, object operand)
    {
        Require(operand is Label, "Conversation branch label is unresolved.");
        Label label = (Label)operand;
        int[] found = Enumerable.Range(0, il.Count).Where(i => il[i].labels.Contains(label)).ToArray();
        Require(found.Length == 1, "Conversation branch must have one target.");
        return found[0];
    }

    private static int Local(CodeInstruction instruction, bool store = false)
    {
        OpCode code = instruction.opcode;
        if (code == (store ? OpCodes.Stloc_0 : OpCodes.Ldloc_0)) return 0;
        if (code == (store ? OpCodes.Stloc_1 : OpCodes.Ldloc_1)) return 1;
        if (code == (store ? OpCodes.Stloc_2 : OpCodes.Ldloc_2)) return 2;
        if (code == (store ? OpCodes.Stloc_3 : OpCodes.Ldloc_3)) return 3;
        if (code != (store ? OpCodes.Stloc : OpCodes.Ldloc) && code != (store ? OpCodes.Stloc_S : OpCodes.Ldloc_S)) return -1;
        return instruction.operand switch
        {
            LocalBuilder variable => variable.LocalIndex,
            LocalVariableInfo variable => variable.LocalIndex,
            int value => value,
            byte value => value,
            _ => -1
        };
    }

    private static int Dispatch(IReadOnlyList<CodeInstruction> il, int id)
    {
        int[] found = Enumerable.Range(5, il.Count - 5).Where(i =>
            il[i].operand is MethodInfo method && method.Name == "CallStaticHook" &&
            il[i - 5].opcode == OpCodes.Ldc_I4 && Equals(il[i - 5].operand, id)).ToArray();
        Require(found.Length == 1, $"Expected exactly one conversation dispatch for {unchecked((uint)id)}.");
        int call = found[0];
        Require(il[call - 4].opcode == OpCodes.Ldarg_0 && il[call - 3].opcode == OpCodes.Ldarg_1 &&
                Local(il[call - 2]) == 0 && Local(il[call - 1]) == 14,
                "Conversation callback arguments must remain NPC, owning player, active conversation and selected response.");
        MethodInfo dispatch = (MethodInfo)il[call].operand;
        Require(dispatch.ReturnType == typeof(object) && dispatch.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                new[] { typeof(uint), typeof(object), typeof(object), typeof(object), typeof(object) }),
                "Conversation callback must retain its object-valued four-argument contract.");
        return call;
    }

    internal static void Verify(IReadOnlyList<CodeInstruction> il, MethodBase method)
    {
        Require(method.DeclaringType?.FullName == "NPCTalking" && method.Name == "Server_HandleResponse" &&
                method.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(new[] {
                    "BasePlayer", "System.Int32", "System.Int32", "System.UInt32", "System.UInt32", "NetworkableId" }),
                "Conversation hooks must use the native typed Server_HandleResponse entrypoint.");
        var locals = method.GetMethodBody()!.LocalVariables;
        Require(locals.Count > 14 && locals[0].LocalType.FullName == "ConversationData" &&
                locals[14].LocalType.FullName == "ConversationData+ResponseNode",
                "Conversation and response local bindings changed.");
        MethodInfo? rpc = method.GetParameters()[0].ParameterType.GetMethod("Server_ConversationResponse",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Require(rpc != null && rpc.GetCustomAttributesData().Any(attribute =>
                attribute.AttributeType.FullName == "BaseEntity+RPC_Server+FromOwner"),
                "Conversation response RPC must retain native owner admission.");
        List<CodeInstruction> rpcIl = PatchProcessor.GetOriginalInstructions(rpc!, out _);
        Require(One(rpcIl, "NPCTalking", "Server_HandleResponse") > 0,
                "Owning player RPC must route through the native typed conversation handler.");
        int respond = Dispatch(il, RespondId), responded = Dispatch(il, RespondedId);
        int start = respond - 5;
        int drive = One(il, "NPCTalking", "CanDriveConversation");
        Require(drive == 2 && il[0].opcode == OpCodes.Ldarg_0 && il[1].opcode == OpCodes.Ldarg_1 &&
                (il[drive + 1].opcode == OpCodes.Brtrue || il[drive + 1].opcode == OpCodes.Brtrue_S) &&
                Target(il, il[drive + 1].operand) == drive + 3 && il[drive + 2].opcode == OpCodes.Ret,
                "Native distance/phone conversation admission must precede every response hook.");
        int speech = One(il, "BasePlayer", "get_ConversationSpeechNodeIndex");
        Require(speech < start && il[speech - 2].opcode == OpCodes.Ldarg_2 && il[speech - 1].opcode == OpCodes.Ldarg_1 &&
                (il[speech + 1].opcode == OpCodes.Beq || il[speech + 1].opcode == OpCodes.Beq_S) &&
                Target(il, il[speech + 1].operand) == speech + 3 && il[speech + 2].opcode == OpCodes.Ret,
                "Stale speech-node requests must return before the response hooks.");
        int conversation = One(il, "NPCTalking", "GetActiveConversationFor");
        Require(conversation < start && Local(il[conversation + 1], true) == 0 && Local(il[conversation + 2]) == 0 &&
                il[conversation + 3].opcode == OpCodes.Ldnull && Calls(il[conversation + 4], "UnityEngine.Object", "op_Equality") &&
                (il[conversation + 5].opcode == OpCodes.Brfalse || il[conversation + 5].opcode == OpCodes.Brfalse_S) &&
                Target(il, il[conversation + 5].operand) == conversation + 7 && il[conversation + 6].opcode == OpCodes.Ret,
                "Only a valid native active conversation may reach response hooks.");
        int visible = One(il, "BasePlayer", "CanSeeConversationResponse");
        Require(visible < start && il[visible - 2].opcode == OpCodes.Ldarg_1 && il[visible - 1].opcode == OpCodes.Ldarg_3 &&
                (il[visible + 1].opcode == OpCodes.Brtrue || il[visible + 1].opcode == OpCodes.Brtrue_S) &&
                Target(il, il[visible + 1].operand) == visible + 3 && il[visible + 2].opcode == OpCodes.Ret,
                "Hidden or invalid response choices must return before the response hook.");
        Require(start >= 14 && Local(il[start - 3], true) == 14 && Local(il[start - 2]) == 14 &&
                (il[start - 1].opcode == OpCodes.Brfalse || il[start - 1].opcode == OpCodes.Brfalse_S) &&
                Target(il, il[start - 1].operand) > respond && il[start - 14].opcode == OpCodes.Ldarg_3 &&
                il[start - 11].opcode == OpCodes.Ldlen && il[start - 10].opcode == OpCodes.Conv_I4 &&
                (il[start - 9].opcode == OpCodes.Blt || il[start - 9].opcode == OpCodes.Blt_S) &&
                Target(il, il[start - 9].operand) == start - 7 && il[start - 8].opcode == OpCodes.Ret,
                "Response callback must follow native response bounds and non-null checks.");
        Require(il[respond + 1].opcode == OpCodes.Ldnull &&
                (il[respond + 2].opcode == OpCodes.Beq || il[respond + 2].opcode == OpCodes.Beq_S) &&
                Target(il, il[respond + 2].operand) == respond + 4 && il[respond + 3].opcode == OpCodes.Ret &&
                Local(il[respond + 4]) == 14 && il[respond + 5].operand is FieldInfo conditions && conditions.Name == "conditions",
                "Every non-null response veto must return; null alone continues native condition evaluation.");
        int pass = One(il, "ConversationData+ResponseNode", "PassesConditions");
        int action = One(il, "NPCTalking", "OnConversationAction");
        Require(respond < pass && pass < action && action < responded,
                "Response veto must precede native conditions and action effects.");
        int end = One(il, "NPCTalking", "ForceEndConversation");
        int next = One(il, "NPCTalking", "ForceSpeechNode");
        Require(end < next && il[end + 1].opcode == OpCodes.Ret && next == responded - 6 &&
                il[responded + 1].opcode == OpCodes.Pop && il[responded + 2].opcode == OpCodes.Ret,
                "Response completion notification must follow ForceSpeechNode; ended conversations must return without it.");
    }
}
