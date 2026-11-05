using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

// Validates the actual rewritten IL, not just successful Harmony installation.
internal static class SpawnHookSemantics
{
    internal const int ThrownId = 1930466752;
    internal const int DroppedId = 565209634;
    internal const int BuiltId = 641201665;

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static bool Calls(CodeInstruction instruction, string type, string method) =>
        (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
        instruction.operand is MethodBase target && target.DeclaringType?.FullName == type && target.Name == method;

    private static int OneCall(IReadOnlyList<CodeInstruction> il, string type, string method)
    {
        int[] found = Enumerable.Range(0, il.Count).Where(i => Calls(il[i], type, method)).ToArray();
        Require(found.Length == 1, $"Expected one {type}.{method} call, found {found.Length}.");
        return found[0];
    }

    private static int LocalOperand(object operand) => operand switch
    {
        LocalBuilder local => local.LocalIndex,
        LocalVariableInfo local => local.LocalIndex,
        int index => index,
        byte index => index,
        short index => index,
        _ => -1
    };

    private static int LoadLocal(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Ldloc_0) return 0;
        if (instruction.opcode == OpCodes.Ldloc_1) return 1;
        if (instruction.opcode == OpCodes.Ldloc_2) return 2;
        if (instruction.opcode == OpCodes.Ldloc_3) return 3;
        return instruction.opcode == OpCodes.Ldloc || instruction.opcode == OpCodes.Ldloc_S
            ? LocalOperand(instruction.operand) : -1;
    }

    private static int StoreLocal(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Stloc_0) return 0;
        if (instruction.opcode == OpCodes.Stloc_1) return 1;
        if (instruction.opcode == OpCodes.Stloc_2) return 2;
        if (instruction.opcode == OpCodes.Stloc_3) return 3;
        return instruction.opcode == OpCodes.Stloc || instruction.opcode == OpCodes.Stloc_S
            ? LocalOperand(instruction.operand) : -1;
    }

    private static bool AddressLocal(CodeInstruction instruction, int index) =>
        (instruction.opcode == OpCodes.Ldloca || instruction.opcode == OpCodes.Ldloca_S) &&
        LocalOperand(instruction.operand) == index;

    private static int LoadArgument(CodeInstruction instruction, MethodBase method)
    {
        if (instruction.opcode == OpCodes.Ldarg_0) return 0;
        if (instruction.opcode == OpCodes.Ldarg_1) return 1;
        if (instruction.opcode == OpCodes.Ldarg_2) return 2;
        if (instruction.opcode == OpCodes.Ldarg_3) return 3;
        if (instruction.opcode != OpCodes.Ldarg && instruction.opcode != OpCodes.Ldarg_S) return -1;
        return instruction.operand is ParameterInfo parameter
            ? parameter.Position + (method.IsStatic ? 0 : 1) : LocalOperand(instruction.operand);
    }

    private static int Target(IReadOnlyList<CodeInstruction> il, object operand)
    {
        Require(operand is Label, "Expected a resolved branch label.");
        Label label = (Label)operand;
        int[] found = Enumerable.Range(0, il.Count).Where(i => il[i].labels.Contains(label)).ToArray();
        Require(found.Length == 1, "Branch target must identify one instruction.");
        return found[0];
    }

    private static void Dominates(IReadOnlyList<CodeInstruction> il, int anchor, int destination, string reason)
    {
        Require(anchor >= 0 && anchor < destination, reason);
        var pending = new Queue<int>();
        var visited = new HashSet<int>();
        pending.Enqueue(0);
        while (pending.Count > 0)
        {
            int i = pending.Dequeue();
            if (i == anchor || i < 0 || i >= il.Count || !visited.Add(i)) continue;
            Require(i != destination, reason);
            FlowControl flow = il[i].opcode.FlowControl;
            if (flow == FlowControl.Return || flow == FlowControl.Throw) continue;
            if (flow == FlowControl.Branch || flow == FlowControl.Cond_Branch)
            {
                if (il[i].operand is Label[] labels)
                    foreach (Label label in labels) pending.Enqueue(Target(il, label));
                else pending.Enqueue(Target(il, il[i].operand));
                if (flow == FlowControl.Branch) continue;
            }
            pending.Enqueue(i + 1);
        }
    }

    private static int Dispatch(IReadOnlyList<CodeInstruction> il, int id, int argumentCount)
    {
        int[] matches = Enumerable.Range(0, il.Count).Where(i =>
            Calls(il[i], "Carbon.HookCaller", "CallStaticHook") && i >= argumentCount + 1 &&
            Enumerable.Range(Math.Max(0, i - 8), Math.Min(i, 8)).Any(j =>
                il[j].opcode == OpCodes.Ldc_I4 && il[j].operand is int value && value == id)).ToArray();
        Require(matches.Length == 1, $"Expected one hook dispatch for {id}.");
        int index = matches[0];
        var call = (MethodInfo)il[index].operand;
        var parameters = call.GetParameters();
        Require(call.ReturnType == typeof(object) && parameters.Length == argumentCount + 1 &&
                parameters[0].ParameterType == typeof(uint) &&
                parameters.Skip(1).All(p => p.ParameterType == typeof(object)), "Hook dispatch argument signature changed.");
        Require(index + 1 < il.Count && il[index + 1].opcode == OpCodes.Pop,
                "Notification hook must discard its return and continue native work.");
        return index;
    }

    private static bool RpcPlayer(CodeInstruction load, CodeInstruction field, MethodBase method) =>
        load.opcode == OpCodes.Ldarg_1 && field.opcode == OpCodes.Ldfld && field.operand is FieldInfo member &&
        member.Name == "player" && member.FieldType.FullName == "BasePlayer" &&
        method.GetParameters().Length > 0 && member.DeclaringType == method.GetParameters()[0].ParameterType;

    private static void VerifyNativeSpawnHelper(MethodInfo helper)
    {
        Require(helper.IsStatic && helper.ReturnType == typeof(void) &&
                helper.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "BaseEntity", "BasePlayer", "System.UInt32" }),
                "SpawnThrownEntity signature changed.");
        List<CodeInstruction> il = PatchProcessor.GetOriginalInstructions(helper, out _);
        int spawn = OneCall(il, "BaseNetworkable", "Spawn");
        Require(spawn > 0 && il[spawn - 1].opcode == OpCodes.Ldarg_0, "Native spawn must use the exact supplied entity.");
        int[] returns = Enumerable.Range(0, il.Count).Where(i => il[i].opcode == OpCodes.Ret).ToArray();
        Require(returns.Length == 1, "Spawn helper has an unexpected early-return path.");
        Dominates(il, spawn, returns[0], "Native entity spawn must occur before the helper returns.");
    }

    internal static void Explosive(IReadOnlyList<CodeInstruction> il, MethodBase method, bool dropped)
    {
        int id = dropped ? DroppedId : ThrownId;
        int dispatch = Dispatch(il, id, 3);
        int start = dispatch - 5;
        Require(start >= 0 && il[start].opcode == OpCodes.Ldc_I4 && Equals(il[start].operand, id) &&
                RpcPlayer(il[start + 1], il[start + 2], method) && il[dispatch - 1].opcode == OpCodes.Ldarg_0,
                "Explosive arguments must be the RPC player, spawned entity, and original ThrownWeapon.");
        int entity = LoadLocal(il[dispatch - 2]);
        MethodBody body = method.GetMethodBody()!;
        Require(entity >= 0 && entity < body.LocalVariables.Count && body.LocalVariables[entity].LocalType.FullName == "BaseEntity",
                "Explosive argument is not a BaseEntity local (throwID is not an entity).");
        int consume = OneCall(il, "HeldEntity", "UseItemAmount");
        Require(consume == dispatch + 5 && il[dispatch + 2].opcode == OpCodes.Ldarg_0 &&
                il[dispatch + 3].opcode == OpCodes.Ldc_I4_1 &&
                il[dispatch + 4].opcode == (dropped ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0),
                "Explosive notification must immediately precede native item consumption with the native ownership flag.");
        if (dropped)
        {
            int spawn = OneCall(il, "ThrownWeapon", "SpawnThrownEntity");
            int setup = OneCall(il, "ThrownWeapon", "SetUpThrownWeapon");
            int cooldown = OneCall(il, "AttackEntity", "StartAttackCooldown");
            Require(spawn >= 4 && LoadLocal(il[spawn - 4]) == entity && RpcPlayer(il[spawn - 3], il[spawn - 2], method),
                    "Drop hook must receive the same entity/player as native spawning.");
            int throwId = LoadLocal(il[spawn - 1]);
            Require(throwId >= 0 && body.LocalVariables[throwId].LocalType == typeof(uint), "Drop prediction ID local changed.");
            int create = OneCall(il, "GameManager", "CreateEntity");
            Require(StoreLocal(il[create + 1]) == entity, "Drop hook entity differs from the created entity.");
            Dominates(il, spawn, start, "Drop notification is reachable before native spawn.");
            Dominates(il, setup, start, "Drop notification is reachable before native setup.");
            Dominates(il, cooldown, start, "Drop notification is reachable before native cooldown.");
            VerifyNativeSpawnHelper((MethodInfo)il[spawn].operand);
        }
        else
        {
            int helper = OneCall(il, "ThrownWeapon", "DoThrowImpl");
            var helperMethod = (MethodInfo)il[helper].operand;
            Require(helperMethod.GetParameters().Length == 8 &&
                    helperMethod.GetParameters()[3].ParameterType == body.LocalVariables[entity].LocalType.MakeByRefType() &&
                    Enumerable.Range(Math.Max(0, helper - 14), Math.Min(helper, 14)).Count(i => AddressLocal(il[i], entity)) == 1,
                    "Throw hook entity is not the exact out-local supplied to DoThrowImpl.");
            Require(helper + 4 < il.Count && LoadLocal(il[helper + 1]) == entity && il[helper + 2].opcode == OpCodes.Ldnull &&
                    Calls(il[helper + 3], "UnityEngine.Object", "op_Equality") &&
                    (il[helper + 4].opcode == OpCodes.Brfalse || il[helper + 4].opcode == OpCodes.Brfalse_S) &&
                    Target(il, il[helper + 4].operand) == start,
                    "Throw notification must begin the non-null spawned-entity success branch; a null result must bypass it.");
            Require(start > helper + 5 && il[start - 1].opcode == OpCodes.Ret &&
                    Enumerable.Range(helper + 5, start - helper - 5).Any(i => Calls(il[i], "ThrownWeapon", "SendThrowResult")),
                    "Failed creation must acknowledge null and return before the notification.");
            Dominates(il, helper, start, "Throw notification is reachable before DoThrowImpl.");
            List<CodeInstruction> inner = PatchProcessor.GetOriginalInstructions(helperMethod, out _);
            int spawn = OneCall(inner, "ThrownWeapon", "SpawnThrownEntity");
            int setup = OneCall(inner, "ThrownWeapon", "SetUpThrownWeapon");
            int cooldown = OneCall(inner, "AttackEntity", "StartAttackCooldown");
            int create = OneCall(inner, "GameManager", "CreateEntity");
            int temporaryEntity = StoreLocal(inner[create + 2]);
            int nativeEntity = StoreLocal(inner[create + 5]);
            Require(create >= 4 && LoadArgument(inner[0], helperMethod) == 4 && inner[1].opcode == OpCodes.Ldnull &&
                    inner[2].opcode == OpCodes.Stind_Ref && LoadArgument(inner[3], helperMethod) == 4 &&
                    inner[create + 1].opcode == OpCodes.Dup && temporaryEntity >= 0 &&
                    inner[create + 3].opcode == OpCodes.Stind_Ref && LoadLocal(inner[create + 4]) == temporaryEntity &&
                    nativeEntity >= 0 && inner.Count(i => i.opcode == OpCodes.Stind_Ref) == 2 &&
                    inner.Count(i => StoreLocal(i) == nativeEntity) == 1 &&
                    LoadLocal(inner[spawn - 3]) == nativeEntity && LoadArgument(inner[spawn - 2], helperMethod) == 3 &&
                    LoadArgument(inner[spawn - 1], helperMethod) == 8 && LoadLocal(inner[setup - 2]) == nativeEntity,
                    "DoThrowImpl must return, spawn and set up the same created entity for the supplied owning player/prediction ID.");
            int lastReturn = Enumerable.Range(0, inner.Count).Last(i => inner[i].opcode == OpCodes.Ret);
            Dominates(inner, spawn, setup, "DoThrowImpl setup is reachable before native spawn.");
            Dominates(inner, setup, cooldown, "DoThrowImpl cooldown is reachable before native setup.");
            Dominates(inner, cooldown, lastReturn, "DoThrowImpl success can return before cooldown.");
            VerifyNativeSpawnHelper((MethodInfo)inner[spawn].operand);
        }
    }

    internal static void Built(IReadOnlyList<CodeInstruction> il, MethodBase method)
    {
        int dispatch = Dispatch(il, BuiltId, 2);
        int start = dispatch - 3;
        int gameObject = LoadLocal(il[dispatch - 1]);
        Require(start >= 4 && il[start].opcode == OpCodes.Ldc_I4 && Equals(il[start].operand, BuiltId) &&
                il[dispatch - 2].opcode == OpCodes.Ldarg_0 && gameObject >= 0 &&
                method.GetMethodBody()!.LocalVariables[gameObject].LocalType.FullName == "UnityEngine.GameObject",
                "OnEntityBuilt must receive Planner and the placed GameObject.");
        Require(LoadLocal(il[start - 4]) == gameObject && il[start - 3].opcode == OpCodes.Ldnull &&
                Calls(il[start - 2], "UnityEngine.Object", "op_Inequality") &&
                (il[start - 1].opcode == OpCodes.Brfalse || il[start - 1].opcode == OpCodes.Brfalse_S) &&
                Target(il, il[start - 1].operand) > dispatch,
                "OnEntityBuilt must be the first operation in the non-null placement branch.");
        int placement = OneCall(il, "Planner", "DoPlacement");
        Require(StoreLocal(il[placement + 1]) == gameObject, "OnEntityBuilt GameObject differs from DoPlacement result.");
        int built = OneCall(il, "Planner", "OnConstructionBuilt");
        Dominates(il, placement, start, "OnEntityBuilt is reachable before completed placement.");
        Dominates(il, dispatch, built, "Successful construction helper can bypass OnEntityBuilt.");
        List<CodeInstruction> placementBody = PatchProcessor.GetOriginalInstructions((MethodInfo)il[placement].operand, out _);
        int spawnHelper = OneCall(placementBody, "Planner", "SpawnConstruction");
        List<CodeInstruction> spawnBody = PatchProcessor.GetOriginalInstructions((MethodInfo)placementBody[spawnHelper].operand, out _);
        int nativeSpawn = OneCall(spawnBody, "BaseNetworkable", "Spawn");
        int[] returnedObjects = Enumerable.Range(0, spawnBody.Count - 1).Where(i =>
            Calls(spawnBody[i], "UnityEngine.Component", "get_gameObject") && spawnBody[i + 1].opcode == OpCodes.Ret).ToArray();
        Require(returnedObjects.Length == 1, "SpawnConstruction must return exactly one successful GameObject value.");
        int returnedObject = returnedObjects[0];
        Require(returnedObject > 0 && spawnBody[returnedObject - 1].opcode == OpCodes.Ldarg_1 &&
                spawnBody[nativeSpawn - 1].opcode == OpCodes.Ldarg_1,
                "SpawnConstruction must return the GameObject of the exact spawned entity.");
        Dominates(spawnBody, nativeSpawn, returnedObject, "Placed GameObject can return before native spawn.");
        List<CodeInstruction> builtBody = PatchProcessor.GetOriginalInstructions((MethodInfo)il[built].operand, out _);
        OneCall(builtBody, "Planner", "PayForPlacement");
    }
}
