using YamlDotNet.Serialization;

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

    [YamlMember(Alias = "optionalDependencies", ApplyNamingConventions = false)]
    public Dictionary<string, string> OptionalDependencies { get; set; } = new();
    public int LoadOrder { get; set; } = 100;
    public string Validation { get; set; } = "strict";
    public PackContentPaths Content { get; set; } = new();

    /// <summary>Absolute directory the pack was loaded from. Set by PackLoader at load time; not deserialized from the manifest file.</summary>
    [YamlDotNet.Serialization.YamlIgnore]
    public string PackDirectory { get; set; } = "";
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
    public string Quests { get; set; } = "";
    public string Motd { get; set; } = "";
    public string MotdColor { get; set; } = "";
}
