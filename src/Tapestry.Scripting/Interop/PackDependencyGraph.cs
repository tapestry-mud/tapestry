namespace Tapestry.Scripting.Interop;

/// <summary>
/// The declared-dependency edge gate for pack interop. Built once at boot from the
/// per-pack dependency map (required + optional deps), in namespace form
/// (e.g. "tapestry-cooking"). Reused by <c>tapestry.packs.call</c> / <c>has</c>.
/// </summary>
public sealed class PackDependencyGraph
{
    private Dictionary<string, HashSet<string>> _edges = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _loaded = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Replace the graph. Keys are loaded pack namespaces; values are the namespaces
    /// each pack declares as a dependency (required + optional, unioned by the caller).
    /// </summary>
    public void Build(IReadOnlyDictionary<string, List<string>> depMap)
    {
        _edges = depMap.ToDictionary(
            kv => kv.Key,
            kv => new HashSet<string>(kv.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        _loaded = new HashSet<string>(depMap.Keys, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>True if <paramref name="fromPack"/> declares a dependency edge on <paramref name="toPack"/>. Literal — self-edges are the caller's concern.</summary>
    public bool DeclaresEdge(string fromPack, string toPack) =>
        _edges.TryGetValue(fromPack, out var deps) && deps.Contains(toPack);

    /// <summary>True if the pack was loaded this boot.</summary>
    public bool IsLoaded(string packNamespace) => _loaded.Contains(packNamespace);
}
