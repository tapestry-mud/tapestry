namespace Tapestry.Engine;

public class AreaRegistry
{
    private readonly Dictionary<string, AreaDefinition> _areas =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(AreaDefinition def) { _areas[def.Id] = def; }
    public AreaDefinition? Get(string id) { return _areas.GetValueOrDefault(id); }
    public IEnumerable<AreaDefinition> All() { return _areas.Values; }
    public bool Contains(string id) { return _areas.ContainsKey(id); }

    /// <summary>Removes the area definition. Returns false when the id was not registered.
    /// Idempotent: a second call on the same id is not an error.</summary>
    public bool Unregister(string id) { return _areas.Remove(id); }
}
