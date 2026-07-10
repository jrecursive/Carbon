namespace Carbon.Hooks;

internal enum StagingHookCompatAction
{
    Shim,
    GeneratorPolicyFix,
    DisabledObsolete
}

internal readonly struct StagingHookCompatEntry
{
    public string HookFullName => string.Empty;
    public StagingHookCompatAction Action => StagingHookCompatAction.GeneratorPolicyFix;
    public bool SuppressGeneratedHook => false;
    public string Reason => string.Empty;
}

internal static class StagingHookCompatManifest
{
    public static IReadOnlyList<StagingHookCompatEntry> All { get; } = Array.Empty<StagingHookCompatEntry>();

    public static bool TryGet(string hookFullName, out StagingHookCompatEntry entry)
    {
        entry = default;
        return false;
    }

    public static bool ShouldSuppressGeneratedHook(string hookFullName, out StagingHookCompatEntry entry)
    {
        entry = default;
        return false;
    }
}
