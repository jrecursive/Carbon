using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using Carbon.StagingHookVerifier;
using HarmonyLib;

internal static class Tests
{
    private static int Main(string[] args)
    {
        string managed = args[0];
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string path = Path.Combine(managed, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        Assembly game = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        MethodInfo target = game.GetType("NPCTalking")!.GetMethod("Server_HandleResponse")!;
        List<CodeInstruction> Build(bool completed = true)
        {
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
            List<CodeInstruction> basic = GeneratedVeto.Transpiler(original, generator, target).ToList();
            return completed ? GeneratedCompleted.Transpiler(basic, generator, target).ToList() : basic;
        }
        ConversationResponseSemantics.Verify(Build(), target);
        int passed = 1;
        Console.WriteLine("PASS actual generated response composition: owner/native admission, veto and completed/ended ordering");
        var harmony = new Harmony("conversation.semantic.install");
        harmony.Patch(target, transpiler: new HarmonyMethod(typeof(GeneratedVeto).GetMethod("Transpiler")));
        harmony.Patch(target, transpiler: new HarmonyMethod(typeof(GeneratedCompleted).GetMethod("Transpiler")));
        harmony.UnpatchAll("conversation.semantic.install");
        passed++;
        Console.WriteLine("PASS both actual generated transpilers install without invalid IL");
        int Hook(List<CodeInstruction> il, int id) => Enumerable.Range(5, il.Count - 5).Single(i =>
            il[i].operand is MethodInfo m && m.Name == "CallStaticHook" &&
            il[i - 5].opcode == OpCodes.Ldc_I4 && Equals(il[i - 5].operand, id));
        int Call(List<CodeInstruction> il, string name) => il.FindIndex(i => i.operand is MethodInfo m && m.Name == name);
        void Reject(string name, Action<List<CodeInstruction>> mutate, bool completed = true)
        {
            List<CodeInstruction> il = Build(completed); mutate(il);
            try { ConversationResponseSemantics.Verify(il, target); }
            catch (InvalidOperationException error)
            {
                Console.WriteLine("PASS reject " + name + ": " + error.Message);
                passed++; return;
            }
            throw new Exception("Unsafe conversation hook mapping accepted: " + name);
        }
        Reject("missing completion dependency", _ => { }, completed: false);
        Reject("wrong owning player argument", il => il[Hook(il, ConversationResponseSemantics.RespondId) - 3].opcode = OpCodes.Ldarg_0);
        Reject("wrong conversation local", il => il[Hook(il, ConversationResponseSemantics.RespondId) - 2].opcode = OpCodes.Ldloc_1);
        Reject("null response enters veto", il => il[Hook(il, ConversationResponseSemantics.RespondId) - 6].opcode = OpCodes.Brtrue_S);
        Reject("reversed null/veto behavior", il => il[Hook(il, ConversationResponseSemantics.RespondId) + 2].opcode = OpCodes.Bne_Un_S);
        Reject("nonnull veto continues native actions", il => il[Hook(il, ConversationResponseSemantics.RespondId) + 3].opcode = OpCodes.Nop);
        Reject("hidden responses admitted", il => il[Call(il, "CanSeeConversationResponse") + 1].opcode = OpCodes.Brfalse_S);
        Reject("stale speech nodes admitted", il => il[Call(il, "get_ConversationSpeechNodeIndex") + 1].opcode = OpCodes.Bne_Un_S);
        Reject("out-of-range response admitted", il => il[Hook(il, ConversationResponseSemantics.RespondId) - 14].opcode = OpCodes.Bge_S);
        Reject("native conversation admission removed", il => { var i = il[Call(il, "CanDriveConversation")]; i.opcode = OpCodes.Nop; i.operand = null; });
        Reject("ended conversation reaches completion", il => il[Call(il, "ForceEndConversation") + 1].opcode = OpCodes.Nop);
        Reject("completion precedes native speech transition", il =>
        {
            int start = Hook(il, ConversationResponseSemantics.RespondedId) - 5;
            List<CodeInstruction> callback = il.GetRange(start, 7);
            il.RemoveRange(start, 7);
            il.InsertRange(Call(il, "OnConversationAction"), callback);
        });
        Console.WriteLine("Conversation semantic tests passed=" + passed);
        return 0;
    }
}

// Bind only the actual generated dispatch signature; no native entity is executed.
public static class HookCaller
{
    public static object? CallStaticHook(uint id, object npc, object player, object conversation, object response) => null;
}
