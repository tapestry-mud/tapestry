namespace Tapestry.Engine.Races;

public static class RaceCostCalculator
{
    public static int AdjustCost(int baseCost, RaceDefinition? race)
    {
        if (race == null) { return baseCost; }
        var adjusted = baseCost + race.CastCostModifier;
        return adjusted < 0 ? 0 : adjusted;
    }
}
