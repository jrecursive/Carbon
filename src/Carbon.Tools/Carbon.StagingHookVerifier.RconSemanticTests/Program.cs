using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using HarmonyLib;
using Carbon.StagingHookVerifier;

internal static class Test
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
            throw new ArgumentException("Expected one managed assembly directory.");
        string managed = args[0];
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string path = Path.Combine(managed, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        Assembly assembly = Assembly.LoadFrom(Path.Combine(managed, "Facepunch.Rcon.dll"));
        MethodInfo target = assembly.GetType("Facepunch.Rcon.Listener")!
            .GetMethod("OnConnection", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;

        List<CodeInstruction> Build(bool includePatch = true)
        {
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
            List<CodeInstruction> basic = GeneratedBase.Transpiler(original, generator, target).ToList();
            return includePatch ? GeneratedPatch.Transpiler(basic, generator, target).ToList() : basic;
        }

        RconHookSemantics.Verify(Build(), target);
        Console.WriteLine("PASS actual generated composition: null retains password check; nonnull profiles/closes/returns before registration; banned sockets bypass hook");
        int passed = 1;
        void Reject(string name, Action<List<CodeInstruction>> mutate, bool includePatch = true)
        {
            List<CodeInstruction> instructions = Build(includePatch);
            mutate(instructions);
            try
            {
                RconHookSemantics.Verify(instructions, target);
            }
            catch (InvalidOperationException error)
            {
                Console.WriteLine("PASS mutation rejected: " + name + " -> " + error.Message);
                passed++;
                return;
            }
            throw new Exception("Accepted unsafe mutation: " + name);
        }
        int Hook(List<CodeInstruction> instructions) => instructions.FindIndex(instruction =>
            instruction.operand is MethodInfo method && method.Name == "CallStaticHook");

        Reject("missing companion patch", _ => { }, includePatch: false);
        Reject("veto jumps to success", instructions =>
        {
            int call = Hook(instructions);
            instructions[call + 2].operand = instructions[call + 9].operand;
        });
        Reject("veto returns without close", instructions =>
        {
            int call = Hook(instructions);
            instructions[call + 2].operand = instructions[^1].labels.First();
        });
        Reject("wrong hook socket argument", instructions => instructions[Hook(instructions) - 3].opcode = OpCodes.Ldarg_0);
        Reject("close call removed", instructions =>
        {
            foreach (CodeInstruction instruction in instructions.Where(instruction =>
                         instruction.operand is MethodInfo method && method.Name == "Close"))
            {
                instruction.opcode = OpCodes.Pop;
                instruction.operand = null;
            }
        });
        Reject("password gate removed", instructions =>
        {
            int call = Hook(instructions);
            instructions[call + 8].opcode = OpCodes.Nop;
            instructions[call + 8].operand = null;
        });
        Console.WriteLine("RCON semantic tests passed=" + passed);
        return 0;
    }
}

// Test-only binding targets used by the actual generated transpiler bodies.
public static class HookCaller
{
    public static object? CallStaticHook(uint id, object value) => null;
}

namespace Carbon.Extensions
{
    public static class AccessToolsEx
    {
        public static Type TypeByName(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name)).First(type => type != null)!;
    }
}
