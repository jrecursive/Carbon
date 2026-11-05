using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Carbon.StagingHookVerifier;

internal static class RconHookSemantics
{
    internal const uint HookId = 3983888645u;

    internal static void Verify(IReadOnlyList<CodeInstruction> il, MethodBase target)
    {
        if (target.DeclaringType?.FullName != "Facepunch.Rcon.Listener" || target.Name != "OnConnection" ||
            target.GetParameters().Length != 1 || target.GetParameters()[0].ParameterType.FullName != "Fleck.IWebSocketConnection")
            throw new InvalidOperationException("RCON admission must target Listener.OnConnection(IWebSocketConnection).");
        var calls = Enumerable.Range(0, il.Count).Where(i => IsCall(il[i], null, "CallStaticHook") && i >= 4 &&
            il[i - 4].opcode == OpCodes.Ldc_I4 && il[i - 4].operand is int value && unchecked((uint)value) == HookId).ToArray();
        if (calls.Length != 1) throw new InvalidOperationException("Expected exactly one web RCON admission hook dispatch.");
        int call = calls[0], start = call - 4;
        var dispatch = (MethodInfo)il[call].operand;
        var args = dispatch.GetParameters();
        if (dispatch.ReturnType != typeof(object) || args.Length != 2 || args[0].ParameterType != typeof(uint) || args[1].ParameterType != typeof(object) ||
            il[call - 3].opcode != OpCodes.Ldarg_1 || !IsCall(il[call - 2], "Fleck.IWebSocketConnection", "get_ConnectionInfo") ||
            !IsCall(il[call - 1], "Fleck.IWebSocketConnectionInfo", "get_ClientIpAddress"))
            throw new InvalidOperationException("Web RCON hook must receive the candidate socket's client IP.");
        if (call + 2 >= il.Count || il[call + 1].opcode != OpCodes.Ldnull ||
            (il[call + 2].opcode != OpCodes.Bne_Un && il[call + 2].opcode != OpCodes.Bne_Un_S))
            throw new InvalidOperationException("Any non-null RCON hook result must branch to native socket rejection.");
        int reject = Target(il, il[call + 2].operand), continuation = call + 3;
        if (continuation + 6 >= il.Count || !IsCall(il[continuation + 2], "Fleck.IWebSocketConnection", "get_ConnectionInfo") ||
            !IsCall(il[continuation + 3], "Fleck.IWebSocketConnectionInfo", "get_Path") ||
            !IsCall(il[continuation + 5], "System.String", "op_Inequality") ||
            (il[continuation + 6].opcode != OpCodes.Brfalse && il[continuation + 6].opcode != OpCodes.Brfalse_S))
            throw new InvalidOperationException("Null RCON hook result must continue native password validation.");
        int success = Target(il, il[continuation + 6].operand);
        if (reject <= continuation || success <= reject || !Enumerable.Range(success, Math.Min(6, il.Count - success)).Any(i => IsCall(il[i], "System.Threading.Interlocked", "Increment")))
            throw new InvalidOperationException("RCON admission success must retain native client ID allocation after rejection.");
        if (il[reject].opcode != OpCodes.Ldloc_0 || il[reject + 1].opcode != OpCodes.Ldfld || il[reject + 1].operand is not FieldInfo socketField || socketField.Name != "socket" || socketField.FieldType.FullName != "Fleck.IWebSocketConnection")
            throw new InvalidOperationException("RCON veto branch must enter with an empty stack at the native socket load.");
        if (!Enumerable.Range(reject, Math.Min(7, il.Count - reject)).Any(i => IsCall(il[i], "Facepunch.Rust.Profiling.RconProfiler", "OnFailedConnection")))
            throw new InvalidOperationException("RCON veto must preserve native failed-connection profiling/accounting.");
        ClosedReturns(il, reject, start, success, true);
        int banned = Enumerable.Range(0, start).Single(i => IsCall(il[i], "Facepunch.Rcon.Listener", "IsBannedIP"));
        if ((il[banned + 1].opcode != OpCodes.Brfalse && il[banned + 1].opcode != OpCodes.Brfalse_S) || Target(il, il[banned + 1].operand) != start)
            throw new InvalidOperationException("Native banned-IP check must precede hook; unbanned sockets must enter hook.");
        ClosedReturns(il, banned + 2, start, success, false);
    }

    private static void ClosedReturns(IReadOnlyList<CodeInstruction> il, int entry, int hookStart, int registration, bool requireProfile)
    {
        var queue = new Queue<(int Index, bool Closed, bool Profiled)>();
        var seen = new HashSet<(int, bool, bool)>();
        queue.Enqueue((entry, false, false)); int returns = 0;
        while (queue.TryDequeue(out var state))
        {
            if (!seen.Add(state)) continue;
            int i = state.Index;
            if (i < 0 || i >= il.Count) throw new InvalidOperationException("RCON rejection falls outside method.");
            if (i == hookStart || i >= registration || IsCall(il[i], "Facepunch.Rust.Profiling.RconProfiler", "OnNewConnection") ||
                (il[i].operand is MethodInfo callback && callback.DeclaringType?.FullName == "Fleck.IWebSocketConnection" && callback.Name.StartsWith("set_On", StringComparison.Ordinal)))
                throw new InvalidOperationException("Rejected RCON socket can reach hook reentry or client/callback registration.");
            bool closed = state.Closed || IsCall(il[i], "Fleck.IWebSocketConnection", "Close");
            bool profiled = state.Profiled || IsCall(il[i], "Facepunch.Rust.Profiling.RconProfiler", "OnFailedConnection");
            if (il[i].opcode == OpCodes.Ret)
            {
                if (!closed || (requireProfile && !profiled)) throw new InvalidOperationException("RCON rejection returns without closing/profiling the socket.");
                returns++; continue;
            }
            if (il[i].opcode.FlowControl == FlowControl.Throw) continue;
            if (il[i].opcode.FlowControl == FlowControl.Branch)
            {
                queue.Enqueue((Target(il, il[i].operand), closed, profiled)); continue;
            }
            if (il[i].opcode.FlowControl == FlowControl.Cond_Branch)
            {
                if (il[i].operand is Label[] labels) foreach (Label label in labels) queue.Enqueue((Target(il, label), closed, profiled));
                else queue.Enqueue((Target(il, il[i].operand), closed, profiled));
            }
            queue.Enqueue((i + 1, closed, profiled));
        }
        if (returns == 0) throw new InvalidOperationException("RCON rejection has no ordinary closed-socket return path.");
    }
    private static bool IsCall(CodeInstruction i, string? type, string name) =>
        (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt) && i.operand is MethodInfo m && m.Name == name && (type == null || m.DeclaringType?.FullName == type);
    private static int Target(IReadOnlyList<CodeInstruction> il, object operand)
    {
        if (operand is not Label label) throw new InvalidOperationException("Expected resolved RCON branch label.");
        var matches = Enumerable.Range(0, il.Count).Where(i => il[i].labels.Contains(label)).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("RCON branch label must identify one instruction.");
        return matches[0];
    }
}
