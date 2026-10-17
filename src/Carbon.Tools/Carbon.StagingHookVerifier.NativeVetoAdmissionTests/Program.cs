using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using Carbon.StagingHookVerifier;
using HarmonyLib;

internal static class Tests
{
    private static int Main(string[] args)
    {
        string managed = Path.GetFullPath(args[0]);
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string path = Path.Combine(managed, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        Assembly native = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        int checks = 0;
        foreach (var spec in new[] {
            ("HackableLockedCrate", "RPC_Hack", typeof(GeneratedCrate), typeof(GeneratedUnsafeCrate)),
            ("FrankensteinTable", "SleepFrankenstein", typeof(GeneratedPet), typeof(GeneratedUnsafePet)) })
        {
            MethodInfo target = native.GetType(spec.Item1)!.GetMethod(spec.Item2, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
            MethodInfo patch = spec.Item3.GetMethod("Transpiler")!;
            List<CodeInstruction> Rewrite(MethodInfo transpiler)
            {
                List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
                return ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { original, generator, target })!).ToList();
            }
            List<CodeInstruction> valid = Rewrite(patch);
            NativeVetoAdmissionSemantics.Verify(valid, target); checks++;
            var harmony = new Harmony("native.veto.admission." + spec.Item1);
            harmony.Patch(target, transpiler: new HarmonyMethod(patch));
            harmony.UnpatchAll(harmony.Id); checks++;
            Console.WriteLine("PASS actual generated " + spec.Item1 + " semantics and native CLR installation");
            void Reject(string description, Action<List<CodeInstruction>> mutation, MethodInfo? unsafePatch = null)
            {
                List<CodeInstruction> il = Rewrite(unsafePatch ?? patch);
                mutation(il);
                try { NativeVetoAdmissionSemantics.Verify(il, target); }
                catch (InvalidOperationException error)
                {
                    Console.WriteLine("PASS reject " + spec.Item1 + " " + description + ": " + error.Message);
                    checks++; return;
                }
                throw new Exception("Unsafe native veto accepted: " + spec.Item1 + " " + description);
            }
            int call = valid.FindIndex(i => i.operand is MethodInfo method && method.Name == "CallStaticHook");
            int start = call - 4;
            Reject("old occupied-stack offset", _ => { }, spec.Item4.GetMethod("Transpiler")!);
            Reject("wrong callback identity", il => il[start + 1].opcode = OpCodes.Ldnull);
            Reject("reversed null admission", il => il[call + 2].opcode = OpCodes.Bne_Un_S);
            Reject("veto reaches native effects", il => il[call + 3].opcode = OpCodes.Nop);
            Reject("native eligibility guard changed", il => il[1].opcode = OpCodes.Nop);
            Reject("native branch bypasses hook", il =>
            {
                Label continuation = (Label)il[call + 2].operand;
                il[spec.Item1 == "HackableLockedCrate" ? 5 : 39].operand = continuation;
            });
        }
        Console.WriteLine("Native veto admission checks passed=" + checks + "; managed=" + managed);
        return 0;
    }
}

public static class HookCaller
{
    public static object? CallStaticHook(uint id, object player, object crate) => null;
    public static object? CallStaticHook(uint id, object pet, object table, object owner) => null;
}
