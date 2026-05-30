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

    /// <summary>
    /// Orders the loaded packs so every pack's dependencies appear before it
    /// (Kahn's algorithm). Ties — packs with no ordering constraint between them —
    /// break alphabetically for determinism, preserving the legacy directory order
    /// for independent packs. Edges pointing at packs that were not loaded are
    /// ignored. If a dependency cycle is present, its members are emitted
    /// alphabetically (best-effort — the edge gate/validator surface the real
    /// problem); the result always contains every loaded pack exactly once.
    /// </summary>
    public IReadOnlyList<string> TopologicalOrder()
    {
        // in-degree = number of this pack's dependencies that were actually loaded.
        var indegree = _loaded.ToDictionary(p => p, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var pack in _loaded)
        {
            foreach (var dep in _edges[pack])
            {
                if (_loaded.Contains(dep))
                {
                    indegree[pack]++;
                }
            }
        }

        // ready = packs whose loaded dependencies are all emitted, picked alphabetically.
        var ready = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in indegree)
        {
            if (kv.Value == 0)
            {
                ready.Add(kv.Key);
            }
        }

        var order = new List<string>(_loaded.Count);
        while (order.Count < _loaded.Count)
        {
            if (ready.Count == 0)
            {
                // Cycle: no pack is fully satisfiable. Break it deterministically by
                // releasing the alphabetically-smallest pack not yet emitted.
                var emitted = new HashSet<string>(order, StringComparer.OrdinalIgnoreCase);
                ready.Add(_loaded.Where(p => !emitted.Contains(p)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).First());
            }

            var next = ready.Min!;
            ready.Remove(next);
            order.Add(next);

            // Releasing `next` may satisfy packs that depend on it.
            foreach (var pack in _loaded)
            {
                if (!order.Contains(pack) && _edges[pack].Contains(next) && --indegree[pack] == 0)
                {
                    ready.Add(pack);
                }
            }
        }

        return order;
    }
}
