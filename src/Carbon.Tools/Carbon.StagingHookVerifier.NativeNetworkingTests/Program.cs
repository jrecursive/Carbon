using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using Carbon.StagingHookVerifier;
using HarmonyLib;

if (args.Length != 2 || args[1] is not ("effects" or "acquaintances"))
    throw new ArgumentException("Expected managed directory and effects|acquaintances.");
string managed = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    string path = Path.Combine(managed, name.Name + ".dll");
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};
Assembly assembly = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
int passed = 0;
void Check(string description, MethodInfo method, Action<IReadOnlyList<CodeInstruction>, MethodInfo> verify)
{
    List<CodeInstruction> Native() => PatchProcessor.GetOriginalInstructions(method, out _);
    verify(Native(), method);
    passed++;
    Console.WriteLine("PASS native " + description);
    void Reject(string mutation, Action<List<CodeInstruction>, int> change)
    {
        var code = Native();
        int limit = code.FindIndex(instruction => instruction.operand is MethodInfo call && call.Name == "get_limitNetworking");
        change(code, limit);
        try { verify(code, method); }
        catch (InvalidOperationException)
        {
            Console.WriteLine("PASS reject " + description + ": " + mutation);
            passed++;
            return;
        }
        throw new InvalidOperationException("Unsafe native networking mutation accepted: " + mutation);
    }
    Reject("reversed limited branch", (code, limit) => code[limit + 1].opcode =
        code[limit + 1].opcode == OpCodes.Brfalse_S ? OpCodes.Brtrue_S : OpCodes.Brfalse_S);
    Reject("wrong peer identity", (code, limit) => code[limit - 1] = new CodeInstruction(OpCodes.Ldarg_0));
    Reject("limited branch returns", (code, limit) => code[limit + 1] = new CodeInstruction(OpCodes.Ret));
}
if (args[1] == "effects")
{
    string[] types = ["Effect+server", "BaseProjectile", "BasePlayer"];
    string[] names = ["ImpactEffect", "ProcessCLProject", "OnAttacked"];
    for (int i = 0; i < types.Length; i++)
    {
        int number = i + 1;
        MethodInfo target = assembly.GetType(types[i])!.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Single(method => method.Name == names[i] && (i != 2 || method.GetParameters() is [{ ParameterType.Name: "HitInfo" }]));
        Check(types[i] + "." + names[i], target, (code, method) => LimitNetworkingSemantics.VerifyNative(number, code, method));
    }
}
else
{
    MethodInfo target = assembly.GetType("RelationshipManager")!.GetMethod("UpdateAcquaintancesFor")!;
    Check("RelationshipManager.UpdateAcquaintancesFor", target, AcquaintanceNetworkingSemantics.VerifyNative);
}
Console.WriteLine("Native networking checks passed=" + passed + "; no game method executed or patched.");
