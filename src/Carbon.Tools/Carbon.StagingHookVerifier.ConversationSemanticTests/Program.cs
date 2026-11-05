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
        return Run(managed);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int Run(string managed)
    {
        Assembly game = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        MethodInfo target = game.GetType("NPCTalking")!.GetMethod("HandleResponse")!;
        List<CodeInstruction> Build(bool completed = true)
        {
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
            List<CodeInstruction> basic = GeneratedVeto.Transpiler(original, generator, target).ToList();
            return completed ? GeneratedCompleted.Transpiler(basic, generator, target).ToList() : basic;
        }
        ConversationResponseSemantics.Verify(Build(), target);
        int passed = 1;
        Console.WriteLine("PASS actual generated composition: native owner/admission/result IL retained, veto and completed/ended ordering");
        var harmony = new Harmony("conversation.semantic.install");
        harmony.Patch(target, transpiler: new HarmonyMethod(typeof(GeneratedVeto).GetMethod("Transpiler")));
        harmony.Patch(target, transpiler: new HarmonyMethod(typeof(GeneratedCompleted).GetMethod("Transpiler")));
        harmony.UnpatchAll("conversation.semantic.install"); passed++;
        Console.WriteLine("PASS both actual generated transpilers install and detach with veto removed first");
        harmony.Patch(target, transpiler: new HarmonyMethod(typeof(GeneratedVeto).GetMethod("Transpiler")));
        harmony.Patch(target, transpiler: new HarmonyMethod(typeof(GeneratedCompleted).GetMethod("Transpiler")));
        harmony.Unpatch(target, typeof(GeneratedCompleted).GetMethod("Transpiler")!);
        harmony.Unpatch(target, typeof(GeneratedVeto).GetMethod("Transpiler")!); passed++;
        Console.WriteLine("PASS completion-first detach preserves the prerequisite and native method");
        int Call(List<CodeInstruction> il, string name) => il.FindIndex(i => i.operand is MethodInfo m && m.Name == name);
        void Reject(string name, Action<List<CodeInstruction>> mutate, bool completed = true)
        {
            List<CodeInstruction> il = Build(completed); mutate(il);
            try { ConversationResponseSemantics.Verify(il, target); }
            catch (InvalidOperationException error)
            { Console.WriteLine("PASS reject " + name + ": " + error.Message); passed++; return; }
            throw new Exception("Unsafe conversation hook mapping accepted: " + name);
        }
        Reject("missing completion dependency", _ => { }, completed: false);
        Reject("wrong owning player argument", il => il[Call(il, "BeforeResponse") - 5].opcode = OpCodes.Ldarg_0);
        Reject("wrong conversation local", il => il[Call(il, "BeforeResponse") - 4].opcode = OpCodes.Ldloc_1);
        Reject("reversed null/veto behavior", il => il[Call(il, "BeforeResponse") + 1].opcode = OpCodes.Brfalse);
        Reject("nonnull veto continues native actions", il => il[Call(il, "BeforeResponse") + 2].opcode = OpCodes.Nop);
        Reject("hidden responses admitted", il => il[Call(il, "CanSeeConversationResponse") + 1].opcode = OpCodes.Brfalse_S);
        Reject("native speech admission removed", il => il[Call(il, "TryGetShownSpeechNode")].opcode = OpCodes.Nop);
        Reject("completion loses selected response", il => il[Call(il, "CompleteResponse") - 1].opcode = OpCodes.Ldnull);
        void RejectHelper(string name, string helperName, Action<List<CodeInstruction>> mutate)
        {
            var il = Build(); MethodInfo helper = (MethodInfo)il[Call(il, helperName)].operand;
            List<CodeInstruction> altered = PatchProcessor.GetOriginalInstructions(helper, out _); mutate(altered);
            try { ConversationResponseSemantics.Verify(il, target, method => method == helper ? altered : PatchProcessor.GetOriginalInstructions(method, out _)); }
            catch (InvalidOperationException error)
            { Console.WriteLine("PASS reject " + name + ": " + error.Message); passed++; return; }
            throw new Exception("Unsafe conversation helper accepted: " + name);
        }
        RejectHelper("ended conversation reaches completion", "CompleteResponse", il => il[Call(il, "OnConversationEnded") + 1].opcode = OpCodes.Nop);
        RejectHelper("completion wrong player", "CompleteResponse", il => il[Call(il, "CallStaticHook") - 3].opcode = OpCodes.Ldarg_0);
        RejectHelper("veto callback wrong conversation", "BeforeResponse", il => il[Call(il, "CallStaticHook") - 2].opcode = OpCodes.Ldarg_1);
        RejectHelper("veto changes null continuation", "BeforeResponse", il => il[Call(il, "CallStaticHook") + 2].opcode = OpCodes.Cgt_Un);
        // Plain authoring node data and the stub dispatcher only; never run a native entity.
        var response = new ConversationData.ResponseNode();
        var speech = new ConversationData.SpeechNodeData { responses = new[] { response } };
        foreach (object? veto in new object?[] { null, false, true, "blocked" })
        {
            HookCaller.Veto = veto; HookCaller.Calls = 0;
            bool result = GeneratedVeto.BeforeResponse(null!, null!, null!, speech, 0, out var selected);
            if (result != (veto == null) || !ReferenceEquals(selected, response) || HookCaller.Calls != 1)
                throw new Exception("Conversation callback null/non-null contract changed.");
            passed++;
        }
        HookCaller.Calls = 0;
        foreach (int index in new[] { -1, 1 })
            if (!GeneratedVeto.BeforeResponse(null!, null!, null!, speech, index, out _) || HookCaller.Calls != 0)
                throw new Exception("Out-of-range response reached callback.");
        speech.responses[0] = null!;
        if (!GeneratedVeto.BeforeResponse(null!, null!, null!, speech, 0, out _) || HookCaller.Calls != 0)
            throw new Exception("Null response reached callback.");
        passed++;
        Console.WriteLine("Conversation semantic tests passed=" + passed);
        return 0;
    }
}
public static class HookCaller
{
    public static object? Veto;
    public static int Calls;
    public static object? CallStaticHook(uint id, object npc, object player, object conversation, object response)
    { Calls++; return Veto; }
}
