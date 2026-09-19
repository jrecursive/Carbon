using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using Carbon.StagingHookVerifier;
using HarmonyLib;

internal static class Tests
{
    private static int passed;
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

    private static int Run(string managed)
    {
        var game = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        MethodInfo target = game.GetType("Planner")!.GetMethod("DoPlacement")!;
        List<CodeInstruction> Build()
        {
            var original = PatchProcessor.GetOriginalInstructions(target, out var generator);
            return ConstructionGeneratedHook.Transpiler(original, generator, target).ToList();
        }
        ConstructionPlacementSemantics.Verify(Build(), target);
        Pass("actual generated callback, null continuation and both native cleanup branches");
        var harmony = new Harmony("construction.semantic.install");
        harmony.Patch(target, transpiler: new HarmonyMethod(typeof(ConstructionGeneratedHook).GetMethod("Transpiler")));
        harmony.UnpatchAll("construction.semantic.install");
        Pass("actual generated transpiler installs in CLR with no invalid synthetic locals");
        int Hook(List<CodeInstruction> il) => il.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType?.FullName == "Oxide.Core.Interface" && m.Name == "CallHook");
        void Reject(string name, Action<List<CodeInstruction>> mutate)
        {
            var il = Build(); mutate(il);
            try { ConstructionPlacementSemantics.Verify(il, target); }
            catch (InvalidOperationException) { Pass("reject " + name); return; }
            throw new Exception("Unsafe construction mapping accepted: " + name);
        }
        Reject("wrong entity", il => il[Hook(il) - 5] = new CodeInstruction(OpCodes.Ldloc_0));
        Reject("wrong owning player", il => il[Hook(il) - 1] = new CodeInstruction(OpCodes.Ldloc_1));
        Reject("reversed null/veto behavior", il => il[Hook(il) + 1].opcode = OpCodes.Brtrue_S);
        Reject("veto returning without cleanup", il => il[Hook(il) + 2].opcode = OpCodes.Ret);
        Reject("cleanup of another object", il =>
        {
            int kill = il.FindIndex(i => i.operand is MethodInfo m && m.Name == "KillMessage");
            il[kill - 1].opcode = OpCodes.Ldloc_0;
        });
        Reject("removed unspawned destruction", il =>
        {
            var destroy = il.Single(i => i.operand is MethodInfo m && m.Name == "EntityDestroy");
            destroy.opcode = OpCodes.Pop; destroy.operand = null;
        });
        // Execute the real generated veto block against instrumented entity doubles.
        // Only native types/methods are substituted; branch, stack and argument IL is unchanged.
        var good = Build();
        int call = Hook(good), start = call - 6;
        Label continueLabel = (Label)good[call + 1].operand;
        int end = good.FindIndex(i => i.labels.Contains(continueLabel));
        var routine = BuildExecutableVeto(good.GetRange(start, end - start), continueLabel);
        foreach (object? value in new object?[] { null, false, true, "deny" })
        foreach (bool spawned in new[] { false, true })
        foreach (bool decay in new[] { false, true })
        {
            FakeEntity entity = decay ? new FakeDecay() : new FakeEntity(); entity.Valid = spawned;
            var player = new object(); var component = new object();
            var placement = new FakeTarget { Marker = 123 };
            Oxide.Core.Interface.Result = value;
            Oxide.Core.Interface.CallCount = 0;
            object? result = routine(null!, placement, component, player, entity);
            var actual = Oxide.Core.Interface.Arguments!;
            if (!ReferenceEquals(actual[0], entity) || !ReferenceEquals(actual[1], component) ||
                actual[2] is not FakeTarget copied || copied.Marker != 123 || !ReferenceEquals(actual[3], player))
                throw new Exception("Callback argument identity was not preserved.");
            if (Oxide.Core.Interface.CallCount != 1 || entity.KillCount != (value != null && spawned ? 1 : 0) ||
                entity.TerminateCount != (value != null && !spawned ? 1 : 0) ||
                entity.DestroyCount != (value != null && !spawned ? 1 : 0) ||
                entity.DecayCount != (value != null && !spawned && decay ? 1 : 0) ||
                entity.SpawnCalls != (value == null ? 1 : 0))
                throw new Exception("Hook, cleanup and native-helper continuation must each execute exactly as selected, without duplicate cleanup or spawn on veto.");
            if (value == null)
            {
                if (!ReferenceEquals(result, entity) || entity.Killed || entity.Terminated || entity.Destroyed || entity.DecayDestroyed)
                    throw new Exception("Null hook result must continue without cleanup.");
            }
            else if (result != null || (spawned ? !entity.Killed : !entity.Terminated || !entity.Destroyed || entity.DecayDestroyed != decay))
                throw new Exception("Any non-null result must clean up and return null, including boxed false.");
        }
        Pass("16 executed null/boxed-false/true/string x valid/unspawned x decay/nondecay veto cases");
        Console.WriteLine("Construction semantic tests passed=" + passed + "; executed veto cases=16");
        return 0;
    }

    private delegate object? Veto(object planner, FakeTarget target, object component, object player, FakeEntity entity);
    private static Veto BuildExecutableVeto(List<CodeInstruction> body, Label continuation)
    {
        var method = new DynamicMethod("ConstructionVeto", typeof(object), new[] { typeof(object), typeof(FakeTarget), typeof(object), typeof(object), typeof(FakeEntity) }, typeof(Tests), true);
        ILGenerator generator = method.GetILGenerator();
        generator.DeclareLocal(typeof(object)); generator.DeclareLocal(typeof(FakeEntity));
        generator.Emit(OpCodes.Ldarg_3); generator.Emit(OpCodes.Stloc_0);
        generator.Emit(OpCodes.Ldarg_S, (byte)4); generator.Emit(OpCodes.Stloc_1);
        var labels = new Dictionary<Label, Label>();
        Label Map(Label label)
        {
            if (!labels.TryGetValue(label, out Label mapped)) labels[label] = mapped = generator.DefineLabel();
            return mapped;
        }
        foreach (CodeInstruction instruction in body)
        {
            foreach (Label label in instruction.labels) generator.MarkLabel(Map(label));
            object operand = instruction.operand;
            if (operand is Label branch) { generator.Emit(instruction.opcode, Map(branch)); continue; }
            if (operand is MethodInfo call)
            {
                MethodInfo substitute = call.Name switch
                {
                    "CallHook" => typeof(Oxide.Core.Interface).GetMethod("CallHook")!,
                    "IsValid" => typeof(Tests).GetMethod(nameof(IsValid), BindingFlags.Static | BindingFlags.NonPublic)!,
                    "op_Implicit" => typeof(Tests).GetMethod(nameof(IsPresent), BindingFlags.Static | BindingFlags.NonPublic)!,
                    "DoServerDestroy" => typeof(FakeDecay).GetMethod(nameof(FakeDecay.DoServerDestroy))!,
                    _ => typeof(FakeEntity).GetMethod(call.Name) ?? throw new Exception("Unexpected cleanup call " + call.Name)
                };
                generator.Emit(instruction.opcode, substitute); continue;
            }
            if (operand is Type type)
            {
                generator.Emit(instruction.opcode, type.Name == "Target" ? typeof(FakeTarget) : typeof(FakeDecay)); continue;
            }
            if (operand is string text) { generator.Emit(instruction.opcode, text); continue; }
            if (operand == null) { generator.Emit(instruction.opcode); continue; }
            throw new Exception("Unexpected veto instruction operand " + operand);
        }
        generator.MarkLabel(Map(continuation));
        generator.Emit(OpCodes.Ldloc_1);
        generator.Emit(OpCodes.Call, typeof(Tests).GetMethod(nameof(ContinuePlacement), BindingFlags.Static | BindingFlags.NonPublic)!);
        generator.Emit(OpCodes.Ret);
        return (Veto)method.CreateDelegate(typeof(Veto));
    }
    private static bool IsValid(FakeEntity entity) => entity.Valid;
    private static bool IsPresent(FakeEntity entity) => entity != null;
    private static object ContinuePlacement(FakeEntity entity) { entity.SpawnCalls++; return entity; }
    private static void Pass(string name) { passed++; Console.WriteLine("PASS " + name); }
}

public struct FakeTarget { public int Marker; }
public class FakeEntity
{
    public bool Valid, Killed, Terminated, Destroyed, DecayDestroyed;
    public int KillCount, TerminateCount, DestroyCount, DecayCount, SpawnCalls;
    public void KillMessage() { Killed = true; KillCount++; }
    public void TerminateOnServer() { Terminated = true; TerminateCount++; }
    public void EntityDestroy() { Destroyed = true; DestroyCount++; }
}
public class FakeDecay : FakeEntity { public void DoServerDestroy() { DecayDestroyed = true; DecayCount++; } }
namespace Oxide.Core
{
    public static class Interface
    {
        public static object? Result;
        public static object[]? Arguments;
        public static int CallCount;
        public static object? CallHook(string name, object entity, object component, object target, object player)
        { CallCount++; Arguments = new[] { entity, component, target, player }; return Result; }
    }
}
namespace Carbon.Extensions
{
    public static class AccessToolsEx
    {
        public static Type TypeByName(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null)!;
    }
}
