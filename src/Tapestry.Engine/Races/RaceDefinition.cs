using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Races;

public class RaceDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public Dictionary<StatType, int> StatCaps { get; init; } = new();
    public int CastCostModifier { get; init; }
    public List<string> RacialFlags { get; init; } = new();
    public string PackName { get; init; } = "";
    public int Priority { get; init; }

    public string Tagline { get; init; } = "";
    public string Description { get; init; } = "";
    public string RaceCategory { get; init; } = "";
    public int StartingAlignment { get; init; }
}
