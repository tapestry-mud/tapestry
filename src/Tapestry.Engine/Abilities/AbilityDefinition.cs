using Tapestry.Engine.Alignment;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Abilities;

public class AbilityDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public AbilityType Type { get; init; }
    public AbilityCategory Category { get; init; }
    public int ResourceCost { get; init; }
    public int PulseDelay { get; init; }
    public bool InitiateOnly { get; init; }
    public int MaxChance { get; init; } = 100;
    public double ProficiencyGainChance { get; init; } = 0.05;
    public double FailureProficiencyGainMultiplier { get; init; } = 0.25;
    public string PackName { get; init; } = "";
    public int Priority { get; init; }
    public string? ShortName { get; init; }
    public string SourceFile { get; init; } = "";
    public AbilityEffectDefinition? Effect { get; init; }
    public List<string> CanTarget { get; init; } = new();
    public Dictionary<string, object?> Metadata { get; init; } = new();
    public object? Handler { get; set; }
    public AlignmentRange? AlignmentRange { get; init; }
    public int Variance { get; init; } = 100;
    public string? GainStat { get; init; }
    public double GainStatScale { get; init; } = 0.0;
    public string? RequiresSlot { get; init; }
    public string? RequiresSlotTag { get; init; }
}

public class AbilityEffectDefinition
{
    public required string EffectId { get; init; }
    public int DurationPulses { get; init; }
    public List<StatModifierDefinition> StatModifiers { get; init; } = new();
    public List<string> Flags { get; init; } = new();
}

public class StatModifierDefinition
{
    public required StatType Stat { get; init; }
    public int Value { get; init; }
}
