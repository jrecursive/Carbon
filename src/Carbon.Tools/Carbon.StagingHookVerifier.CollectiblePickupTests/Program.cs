using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using Carbon.StagingHookVerifier;
using HarmonyLib;

internal static class Tests
{
    private static int Main(string[] args)
    {
        try { return Run(args); }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL collectible pickup: " + error.Message);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        string managed = Path.GetFullPath(args[0]);
        string carbon = Path.GetFullPath(args[1]);
        string hookFile = Path.GetFullPath(args[2]);
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            foreach (string directory in new[] { carbon, Path.Combine(carbon, "lib"), managed })
            {
                string path = Path.Combine(directory, name.Name + ".dll");
                if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
            }
            return null;
        };
        Assembly native = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        Assembly hooks = Assembly.LoadFrom(hookFile);
        MethodInfo target = native.GetType("CollectibleEntity")!.GetMethod("DoPickup")!;
        MethodInfo Transpiler(string name) => hooks.GetTypes().Single(type => type.CustomAttributes.Any(attribute =>
            attribute.AttributeType.FullName == "API.Hooks.HookAttribute+Patch" &&
            attribute.ConstructorArguments[0].Value as string == name)).GetMethod("Transpiler")!;
        MethodInfo before = Transpiler("OnCollectiblePickup"), after = Transpiler("OnCollectiblePickedup");
        List<CodeInstruction> Rewrite()
        {
            List<CodeInstruction> current = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
            foreach (MethodInfo patch in new[] { before, after })
                current = ((IEnumerable<CodeInstruction>)patch.Invoke(null, new object[] { current, generator, target })!).ToList();
            return current;
        }

        int checks = 0;
        CollectiblePickupSemantics.Verify(Rewrite(), target); checks++;
        Console.WriteLine("PASS exact generated composition: player delivery, null receiver, null item, consumed food and multi-output loop");
        var harmony = new Harmony("carbon.collectible.pickup.focused-regression");
        try
        {
            harmony.Patch(target, transpiler: new HarmonyMethod(before, Priority.VeryHigh));
            harmony.Patch(target, transpiler: new HarmonyMethod(after, Priority.VeryHigh));
            Console.WriteLine("PASS exact generated dependency and completion CLR installation"); checks++;
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }

        void Reject(string description, Action<List<CodeInstruction>> mutation)
        {
            List<CodeInstruction> il = Rewrite();
            mutation(il);
            try { CollectiblePickupSemantics.Verify(il, target); }
            catch (InvalidOperationException error)
            {
                Console.WriteLine("PASS reject " + description + ": " + error.Message);
                checks++; return;
            }
            throw new Exception("Unsafe collectible composition accepted: " + description);
        }
        Reject("old ItemModConsume local9 argument", il => il[131].operand = 9);
        Reject("old receiver-null drop placement even with corrected Item argument", il =>
        {
            List<CodeInstruction> block = il.GetRange(128, 6);
            il.RemoveRange(128, 6);
            il.InsertRange(146, block);
        });
        Reject("wrong receiver identity", il => il[130].opcode = OpCodes.Ldnull);
        Reject("notification return changes native delivery", il => il[133].opcode = OpCodes.Ret);
        Reject("receiver-null branch enters player notification", il =>
        {
            Label label = targetLabel(il, 128);
            il[118].operand = label; // native109 + nine entry-veto instructions
        });
        Reject("null-created item enters notification", il => il[86].operand = targetLabel(il, 128));
        Reject("consumed food enters notification", il => il[115].operand = targetLabel(il, 128));
        Reject("output loop bypasses creation", il => il[162].operand = targetLabel(il, 154));
        Reject("pickup veto accepts non-null responses", il => il[11].opcode = OpCodes.Bne_Un_S);
        Reject("native GiveItem changed", il => il[138].opcode = OpCodes.Nop);
        Console.WriteLine("Collectible pickup checks passed=" + checks + "; managed=" + managed + "; hooks=" + hookFile);
        return 0;
    }

    private static Label targetLabel(List<CodeInstruction> il, int index)
    {
        var method = new DynamicMethod("collectible-regression-label", typeof(void), Type.EmptyTypes);
        Label label = method.GetILGenerator().DefineLabel();
        // A label identity contains only its index. Pick a currently unused one
        // so the mutation tests exercise the intended branch, not ambiguity.
        var labels = il.SelectMany(instruction => instruction.labels).ToHashSet();
        ILGenerator generator = method.GetILGenerator();
        while (labels.Contains(label)) label = generator.DefineLabel();
        il[index].labels.Add(label);
        return label;
    }
}
