namespace Tapestry.Shared;

public class PackManifest
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string Copyright { get; set; } = "";
    public string Website { get; set; } = "";
    public string License { get; set; } = "";
    public string EngineVersion { get; set; } = "";
    public bool Active { get; set; } = true;
    public Dictionary<string, string> Dependencies { get; set; } = new();
    public int LoadOrder { get; set; } = 100;
    public PackContentPaths Content { get; set; } = new();
}

public class PackContentPaths
{
    public string Rooms { get; set; } = "";
    public string Items { get; set; } = "";
    public string EquipmentSlots { get; set; } = "";
    public string Recipes { get; set; } = "";
    public string Resources { get; set; } = "";
    public string Scripts { get; set; } = "";
    public string Strings { get; set; } = "";
    public string Mobs { get; set; } = "";
    public string AreaDefinitions { get; set; } = "";
    public string WeatherZones { get; set; } = "";
    public string Help { get; set; } = "";
}
