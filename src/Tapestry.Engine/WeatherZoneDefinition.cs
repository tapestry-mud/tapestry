namespace Tapestry.Engine;

public class WeatherZoneDefinition
{
    public string Id { get; init; } = "";
    public List<string> States { get; init; } = new();
    public Dictionary<string, Dictionary<string, int>> Transitions { get; init; } = new();
    public Dictionary<string, Dictionary<string, WeatherMessages>> TerrainMessages { get; init; } = new();
    public Dictionary<string, Dictionary<string, string>> TerrainTransitions { get; init; } = new();
}
