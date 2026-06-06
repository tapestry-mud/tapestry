namespace Tapestry.Engine;

public class AreaDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; set; } = "";
    public string Short { get; set; } = "";
    public string Description { get; set; } = "";
    public string Theme { get; set; } = "";
    public string Lore { get; set; } = "";
    public int[] LevelRange { get; set; } = [1, 99];
    public int ResetInterval { get; set; } = 3000;
    public float OccupiedModifier { get; set; } = 3.0f;
    public string? WeatherZone { get; set; }
    public List<string> Flags { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> WeatherOverrides { get; init; } = new();
    public Dictionary<string, WeatherMessages> WeatherMessages { get; init; } = new();
    public Dictionary<string, string> TimeMessages { get; init; } = new();

    /// <summary>Pack that loaded this area; null = runtime-authored. Transient, never serialized.</summary>
    public string? SourcePack { get; set; }
}
