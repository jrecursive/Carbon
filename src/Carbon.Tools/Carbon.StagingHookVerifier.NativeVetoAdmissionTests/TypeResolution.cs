namespace Carbon.Extensions;

internal static class AccessToolsEx
{
    public static Type TypeByName(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null)!;
}
