// src/Tapestry.Engine/Progression/TrackDefinition.cs
namespace Tapestry.Engine.Progression;

public class TrackDefinition
{
    public required string Name { get; init; }
    public required int MaxLevel { get; init; }
    public int[]? XpTable { get; init; }
    public Func<int, int>? XpFormula { get; init; }
    public Action<Guid, string, int>? OnLevelUp { get; init; }
    public double DeathPenalty { get; init; } = 0.0;

    /// <summary>
    /// Returns the total XP required to reach the given level.
    /// Table takes priority over formula. Returns -1 if neither is defined.
    /// </summary>
    public int GetXpForLevel(int level)
    {
        if (XpTable != null && level >= 0 && level < XpTable.Length)
        {
            return XpTable[level];
        }

        if (XpFormula != null)
        {
            return XpFormula(level);
        }

        return -1;
    }
}
