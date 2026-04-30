namespace Tapestry.Engine;

public class AreaDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int[] LevelRange { get; init; } = [1, 99];
    public int ResetInterval { get; init; } = 3000;
    public float OccupiedModifier { get; init; } = 3.0f;
    public string? WeatherZone { get; init; }
    public List<string> Flags { get; init; } = new();
    public Dictionary<string, Dictionary<string, int>> WeatherOverrides { get; init; } = new();
    public Dictionary<string, WeatherMessages> WeatherMessages { get; init; } = new();
    public Dictionary<string, string> TimeMessages { get; init; } = new();
}
