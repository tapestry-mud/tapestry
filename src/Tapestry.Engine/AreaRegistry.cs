namespace Tapestry.Engine;

public class AreaRegistry
{
    private readonly Dictionary<string, AreaDefinition> _areas =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(AreaDefinition def) { _areas[def.Id] = def; }
    public AreaDefinition? Get(string id) { return _areas.GetValueOrDefault(id); }
    public IEnumerable<AreaDefinition> All() { return _areas.Values; }
    public bool Contains(string id) { return _areas.ContainsKey(id); }
}
