using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using Carbon.StagingHookVerifier;
using HarmonyLib;

if (args.Length != 1) throw new ArgumentException("Expected a managed assembly directory.");
string managed = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    string path = Path.Combine(managed, name.Name + ".dll");
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};
Assembly assembly = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
MethodInfo target = assembly.GetType("RelationshipManager")!.GetMethod("UpdateAcquaintancesFor")!;
(List<CodeInstruction> Before, List<CodeInstruction> After) Compose()
{
    List<CodeInstruction> source = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
    List<CodeInstruction> before = source.Select(instruction => new CodeInstruction(instruction)).ToList();
    List<CodeInstruction> after = GeneratedAcquaintancePatch.Transpiler(source, generator, target).ToList();
    return (before, after);
}
var correct = Compose();
AcquaintanceNetworkingSemantics.Verify(correct.Before, correct.After, target);
int passed = 1;
Console.WriteLine("PASS actual generated acquaintance guard semantic proof");
void Reject(string name, Action<List<CodeInstruction>, int> mutate)
{
    var pair = Compose();
    int limit = pair.After.FindIndex(instruction => instruction.operand is MethodInfo method && method.Name == "get_limitNetworking");
    mutate(pair.After, limit);
    try { AcquaintanceNetworkingSemantics.Verify(pair.Before, pair.After, target); }
    catch (InvalidOperationException)
    {
        Console.WriteLine("PASS rejected " + name);
        passed++;
        return;
    }
    throw new InvalidOperationException("Unsafe acquaintance mutation accepted: " + name);
}
Reject("wrong peer local", (il, limit) => il[limit - 1] = new CodeInstruction(OpCodes.Ldloc_0));
Reject("reversed limit polarity", (il, limit) => il[limit + 1].opcode = OpCodes.Brfalse);
Reject("skip cleanup by returning", (il, limit) => il[limit + 1] = new CodeInstruction(OpCodes.Ret));
Reject("limited peer enters relation work", (il, limit) =>
{
    Label label = (Label)il[limit + 1].operand;
    il.Single(instruction => instruction.labels.Contains(label)).labels.Remove(label);
    il[limit + 2].labels.Add(label);
});
Reject("native lastSeen store removed", (il, _) =>
    il.Single(instruction => instruction.operand is FieldInfo field && field.Name == "lastSeenTime").opcode = OpCodes.Pop);
Reject("native pooled-list release removed", (il, _) =>
    il.Single(instruction => instruction.operand is MethodInfo method && method.Name == "FreeUnmanaged").opcode = OpCodes.Nop);
Reject("native enumerator disposal removed", (il, _) =>
    il.Single(instruction => instruction.opcode == OpCodes.Endfinally).opcode = OpCodes.Nop);
var harmony = new Harmony("carbon.acquaintance-networking.focused");
MethodInfo patch = typeof(GeneratedAcquaintancePatch).GetMethod("Transpiler")!;
try
{
    harmony.Patch(target, transpiler: new HarmonyMethod(patch));
    Console.WriteLine("PASS generated acquaintance patch CLR installation; game method never executed");
    passed++;
}
finally { harmony.Unpatch(target, patch); }
Console.WriteLine("Acquaintance networking checks passed=" + passed);
