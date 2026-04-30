using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Classes;

public class ClassDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public Dictionary<StatType, string> StatGrowth { get; init; } = new();
    public string PackName { get; init; } = "";
    public int Priority { get; init; }

    public string Tagline { get; init; } = "";
    public string Description { get; init; } = "";
    public string Track { get; init; } = "";
    public int StartingAlignment { get; init; }
    public string LevelUpFlavor { get; init; } = "";
    public List<string> AllowedCategories { get; init; } = new();
    public List<string> AllowedGenders { get; init; } = new();
    public List<ClassPathEntry> Path { get; init; } = new();
    public int TrainsPerLevel { get; init; } = 5;
    public Dictionary<StatType, StatType> GrowthBonuses { get; init; } = new();
}
