using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Carbon.StagingHookVerifier;

internal static class Probe
{
    private static int checks;
    private static List<CodeInstruction> Inject(MethodInfo method, int index, int id, int entity, bool built)
    {
        var il = PatchProcessor.GetOriginalInstructions(method, out _);
        var hook = new List<CodeInstruction> { new(OpCodes.Ldc_I4, id), new(OpCodes.Ldarg_0) };
        if (!built)
        {
            hook[1] = new CodeInstruction(OpCodes.Ldarg_1);
            hook.Add(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(method.GetParameters()[0].ParameterType, "player")));
        }
        hook.Add(new CodeInstruction(OpCodes.Ldloc_S, entity));
        if (!built) hook.Add(new CodeInstruction(OpCodes.Ldarg_0));
        hook.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Carbon.HookCaller), "CallStaticHook",
            built ? new[] { typeof(uint), typeof(object), typeof(object) } : new[] { typeof(uint), typeof(object), typeof(object), typeof(object) })));
        hook.Add(new CodeInstruction(OpCodes.Pop));
        hook[0].labels.AddRange(il[index].labels);
        il[index].labels.Clear();
        il.InsertRange(index, hook);
        return il;
    }

    private static void Check(Action action, bool accepted, string name)
    {
        try
        {
            action();
            if (!accepted) throw new Exception("Invalid mapping accepted: " + name);
        }
        catch (InvalidOperationException error)
        {
            if (accepted) throw new Exception("Valid mapping rejected: " + name + ": " + error.Message, error);
        }
        checks++;
        Console.WriteLine("PASS " + name);
    }

    public static int Main(string[] args)
    {
        string root = args[0];
        AppDomain.CurrentDomain.AssemblyResolve += (_, query) =>
        {
            string name = new AssemblyName(query.Name).Name + ".dll";
            string path = Path.Combine(root, name);
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        var game = Assembly.LoadFrom(Path.Combine(root, "Assembly-CSharp.dll"));
        var type = game.GetType("ThrownWeapon", true)!;
        var throwing = AccessTools.Method(type, "DoThrow");
        var dropping = AccessTools.Method(type, "DoDrop");
        var planner = game.GetType("Planner", true)!;
        var building = planner.GetMethods().Single(m => m.Name == "DoBuild" && m.GetParameters().Length == 2);
        Check(() => SpawnHookSemantics.Explosive(Inject(throwing, 65, SpawnHookSemantics.ThrownId, 4, false), throwing, false), true, "throw success branch after native spawn/setup and before consumption");
        Check(() => SpawnHookSemantics.Explosive(Inject(dropping, 138, SpawnHookSemantics.DroppedId, 3, false), dropping, true), true, "drop exact entity/player after spawn/setup and before consumption");
        Check(() => SpawnHookSemantics.Built(Inject(building, 183, SpawnHookSemantics.BuiltId, 2, true), building), true, "build successful GameObject after spawn before deployment payment");
        Check(() => SpawnHookSemantics.Explosive(Inject(throwing, 44, SpawnHookSemantics.ThrownId, 4, false), throwing, false), false, "reject old throw index before entity creation");
        Check(() => SpawnHookSemantics.Explosive(Inject(throwing, 55, SpawnHookSemantics.ThrownId, 4, false), throwing, false), false, "reject throw on null result before guard");
        Check(() => SpawnHookSemantics.Explosive(Inject(throwing, 69, SpawnHookSemantics.ThrownId, 4, false), throwing, false), false, "reject throw after consumption");
        Check(() => SpawnHookSemantics.Explosive(Inject(dropping, 138, SpawnHookSemantics.DroppedId, 2, false), dropping, true), false, "reject prediction ID as dropped entity");
        Check(() => SpawnHookSemantics.Explosive(Inject(dropping, 124, SpawnHookSemantics.DroppedId, 3, false), dropping, true), false, "reject drop before spawning");
        var bypass = Inject(throwing, 65, SpawnHookSemantics.ThrownId, 4, false);
        bypass[72].labels.AddRange(bypass[65].labels); bypass[65].labels.Clear();
        Check(() => SpawnHookSemantics.Explosive(bypass, throwing, false), false, "reject branch label bypassing notification");
        var wrongPlayer = Inject(throwing, 65, SpawnHookSemantics.ThrownId, 4, false);
        wrongPlayer[66] = new CodeInstruction(OpCodes.Ldarg_0);
        Check(() => SpawnHookSemantics.Explosive(wrongPlayer, throwing, false), false, "reject wrong RPC player source");
        Check(() => SpawnHookSemantics.Built(Inject(building, 179, SpawnHookSemantics.BuiltId, 2, true), building), false, "reject build before nonnull guard");
        Check(() => SpawnHookSemantics.Built(Inject(building, 183, SpawnHookSemantics.BuiltId, 1, true), building), false, "reject build nonGameObject local");
        Console.WriteLine("Spawn semantic regressions passed=" + checks);
        return 0;
    }
}

namespace Carbon
{
    public static class HookCaller
    {
        public static object CallStaticHook(uint id, object a, object b) => null!;
        public static object CallStaticHook(uint id, object a, object b, object c) => null!;
    }
}
