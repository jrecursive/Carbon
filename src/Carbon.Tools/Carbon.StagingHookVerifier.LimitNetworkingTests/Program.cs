using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using HarmonyLib;
using Carbon.StagingHookVerifier;

internal static class Test
{
    private static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Expected a managed assembly directory.");
        string managed = args[0];
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string path = Path.Combine(managed, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        Assembly assembly = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        MethodInfo[] targets =
        [
            assembly.GetType("Effect+server")!.GetMethod("ImpactEffect")!,
            assembly.GetType("BaseProjectile")!.GetMethod("CLProject", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!,
            assembly.GetType("BasePlayer")!.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(method => method.Name == "OnAttacked" && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType.Name == "HitInfo")
        ];
        Type[] bases = [typeof(GeneratedImpactBase), typeof(GeneratedProjectileBase), typeof(GeneratedHeadshotBase)];
        Type[] patches = [typeof(GeneratedImpactPatch), typeof(GeneratedProjectilePatch), typeof(GeneratedHeadshotPatch)];

        (List<CodeInstruction> Before, List<CodeInstruction> After) Compose(int number)
        {
            MethodInfo target = targets[number - 1];
            List<CodeInstruction> source = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
            List<CodeInstruction> basic = ((IEnumerable<CodeInstruction>)bases[number - 1].GetMethod("Transpiler")!
                .Invoke(null, new object[] { source, generator, target })!).ToList();
            List<CodeInstruction> snapshot = basic.Select(instruction => new CodeInstruction(instruction)).ToList();
            List<CodeInstruction> rewritten = ((IEnumerable<CodeInstruction>)patches[number - 1].GetMethod("Transpiler")!
                .Invoke(null, new object[] { basic, generator, target })!).ToList();
            return (snapshot, rewritten);
        }

        int passed = 0;
        for (int number = 1; number <= 3; number++)
        {
            var pair = Compose(number);
            LimitNetworkingSemantics.Verify(number, pair.Before, pair.After, targets[number - 1]);
            Console.WriteLine("PASS actual generated family patch " + number + " semantic proof");
            passed++;
        }
        void Reject(string name, int number, Action<List<CodeInstruction>> mutate)
        {
            var pair = Compose(number);
            mutate(pair.After);
            try
            {
                LimitNetworkingSemantics.Verify(number, pair.Before, pair.After, targets[number - 1]);
            }
            catch (Exception error) when (error is InvalidOperationException || error is ArgumentException)
            {
                Console.WriteLine("PASS rejected " + name + ": " + error.Message);
                passed++;
                return;
            }
            throw new Exception("Unsafe mutation accepted: " + name);
        }
        int Limit(List<CodeInstruction> instructions) => instructions.FindIndex(instruction =>
            instruction.operand is MethodInfo method && method.Name == "get_limitNetworking");

        Reject("headshot wrong initiator local", 3, il => il[Limit(il) - 4] = new CodeInstruction(OpCodes.Ldloc_0));
        Reject("headshot null skips native FX", 3, il => il[Limit(il) - 2].operand = il[Limit(il) + 1].operand);
        Reject("headshot limited returns to FX", 3, il => il[Limit(il) + 1].operand = il[Limit(il) - 2].operand);
        Reject("headshot limit polarity reversed", 3, il => il[Limit(il) + 1].opcode = OpCodes.Brfalse_S);
        Reject("headshot native remainder changed", 3, il => il[^1].opcode = OpCodes.Nop);
        Reject("impact unlimited returns", 1, il => il[Limit(il) + 1].opcode = OpCodes.Brtrue_S);
        Reject("projectile limit polarity reversed", 2, il => il[Limit(il) + 1].opcode = OpCodes.Brfalse_S);

        // The original failure was invalid CLR code after dependency composition.
        // Install the actual generated transpilers, but never execute the game method.
        var harmony = new Harmony("carbon.limitnetworking.focused");
        try
        {
            harmony.Patch(targets[2], transpiler: new HarmonyMethod(bases[2].GetMethod("Transpiler")!));
            harmony.Patch(targets[2], transpiler: new HarmonyMethod(patches[2].GetMethod("Transpiler")!));
            Console.WriteLine("PASS headshot base+patch CLR installation (method never executed)");
            passed++;
        }
        finally
        {
            // Removing the base first would temporarily apply base-relative offsets to native IL.
            harmony.Unpatch(targets[2], patches[2].GetMethod("Transpiler")!);
            harmony.Unpatch(targets[2], bases[2].GetMethod("Transpiler")!);
        }
        Console.WriteLine("LimitNetworking checks passed=" + passed);
        return 0;
    }
}

// Test-only dispatch targets for the actual generated transpiler bodies.
public static class HookCaller
{
    public static object? CallStaticHook(uint id, object a, object b) => null;
    public static object? CallStaticHook(uint id, object a, object b, object c, object d) => null;
}

namespace Carbon.Core
{
    public static class CorePlugin
    {
        public static object? IOnBasePlayerAttacked(object player, object hit) => null;
    }
}

namespace Carbon.Extensions
{
    public static class AccessToolsEx
    {
        public static Type TypeByName(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name)).First(type => type != null)!;
    }
}
