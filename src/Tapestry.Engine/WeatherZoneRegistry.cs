namespace Tapestry.Engine;

public class WeatherZoneRegistry
{
    private readonly Dictionary<string, WeatherZoneDefinition> _zones =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(WeatherZoneDefinition def) { _zones[def.Id] = def; }
    public WeatherZoneDefinition? Get(string id) { return _zones.GetValueOrDefault(id); }
    public IEnumerable<WeatherZoneDefinition> All() { return _zones.Values; }
    public bool Contains(string id) { return _zones.ContainsKey(id); }
}
