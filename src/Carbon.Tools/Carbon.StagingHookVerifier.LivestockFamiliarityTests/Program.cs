using System.Reflection;
using System.Runtime.CompilerServices;
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
        MethodInfo target = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"))
            .GetType("Rust.Ai.Gen2.LivestockAnimal")!.GetMethod("AddFamiliarity")!;
        MethodInfo prefix = typeof(GeneratedAdmission).GetMethod("Prefix")!;
        MethodInfo postfix = typeof(GeneratedCompletion).GetMethod("Postfix")!;
        LivestockFamiliaritySemantics.Verify(target, prefix, postfix);
        int checks = 1;
        var actual = new Harmony("livestock.familiarity.actual.install");
        actual.Patch(target, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
        actual.UnpatchAll("livestock.familiarity.actual.install");
        checks++;
        Console.WriteLine("PASS actual generated prefix/postfix semantics and native CLR installation");
        void Reject(string description, string? admission = null, string? completion = null)
        {
            try
            {
                LivestockFamiliaritySemantics.Verify(target,
                    admission == null ? prefix : typeof(Unsafe).GetMethod(admission)!,
                    completion == null ? postfix : typeof(Unsafe).GetMethod(completion)!);
            }
            catch (InvalidOperationException error)
            {
                Console.WriteLine("PASS reject " + description + ": " + error.Message);
                checks++; return;
            }
            throw new Exception("Unsafe familiarity patch accepted: " + description);
        }
        Reject("reversed nonnull veto", admission: nameof(Unsafe.ReversedAdmission));
        Reject("wrong user identity", admission: nameof(Unsafe.WrongUser));
        Reject("completion after skipped original", completion: nameof(Unsafe.UngatedCompletion));
        Reject("wrong completion callback", completion: nameof(Unsafe.WrongCompletion));
        var fixture = new Harmony("livestock.familiarity.fixture");
        fixture.Patch(typeof(FakeAnimal).GetMethod(nameof(FakeAnimal.AddFamiliarity))!,
            prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
        foreach (var choice in new[] { (0UL, 1f), (42UL, -1f), (42UL, 0f), (42UL, 1f), (42UL, 2f), (42UL, 3f) })
        foreach (object? veto in new object?[] { null, false, true, "deny" })
        {
            var animal = new FakeAnimal();
            HookCaller.Events.Clear(); HookCaller.Veto = veto; HookCaller.Arguments.Clear();
            animal.AddFamiliarity(choice.Item1, choice.Item2);
            bool ran = veto == null;
            string[] expected = ran ? ["adding", "native", "added"] : ["adding"];
            if (!HookCaller.Events.SequenceEqual(expected) || animal.NativeCalls != (ran ? 1 : 0))
                throw new Exception("Veto/completion ordering failed for one native return path.");
            foreach (object[] values in HookCaller.Arguments)
                if (!ReferenceEquals(values[0], animal) || values[1] is not ulong id || id != choice.Item1 ||
                    values[2] is not float seconds || seconds != choice.Item2)
                    throw new Exception("Familiarity callback argument identity changed.");
            checks++;
        }
        HookCaller.Veto = null; HookCaller.Events.Clear();
        try { new FakeAnimal().AddFamiliarity(42, 99); throw new Exception("Expected native fixture failure."); }
        catch (InvalidOperationException error) when (error.Message == "native fixture failure") { }
        if (!HookCaller.Events.SequenceEqual(new[] { "adding", "native" }))
            throw new Exception("Completion dispatched after a native exception.");
        checks++;
        fixture.UnpatchAll("livestock.familiarity.fixture");
        Console.WriteLine("Familiarity semantic checks passed=" + checks + "; six native return paths, null/false/true/string veto, native exception");
        return 0;
    }
}

internal sealed class FakeAnimal
{
    public int NativeCalls;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void AddFamiliarity(ulong userId, float seconds)
    {
        NativeCalls++; HookCaller.Events.Add("native");
        if (userId == 0) return;
        if (seconds < 0) return;
        if (seconds == 0) return;
        if (seconds == 1) return;
        if (seconds == 2) return;
        if (seconds == 99) throw new InvalidOperationException("native fixture failure");
    }
}

public static class HookCaller
{
    public static object? Veto;
    public static readonly List<string> Events = new();
    public static readonly List<object[]> Arguments = new();
    public static object? CallStaticHook(uint id, object animal, object userId, object seconds)
    {
        Events.Add(id == LivestockFamiliaritySemantics.AddingId ? "adding" : id == LivestockFamiliaritySemantics.AddedId ? "added" : "wrong");
        Arguments.Add([animal, userId, seconds]);
        return id == LivestockFamiliaritySemantics.AddingId ? Veto : "discarded completion return";
    }
}

public static class Unsafe
{
    public static bool ReversedAdmission(object __instance, ulong userId, float seconds) =>
        HookCaller.CallStaticHook(2189779015u, __instance, userId, seconds) != null;
    public static bool WrongUser(object __instance, ulong userId, float seconds) =>
        HookCaller.CallStaticHook(2189779015u, __instance, 0UL, seconds) == null;
    public static void UngatedCompletion(object __instance, ulong userId, float seconds, bool __runOriginal) =>
        HookCaller.CallStaticHook(3837166704u, __instance, userId, seconds);
    public static void WrongCompletion(object __instance, ulong userId, float seconds, bool __runOriginal)
    {
        if (__runOriginal) HookCaller.CallStaticHook(2189779015u, __instance, userId, seconds);
    }
}
