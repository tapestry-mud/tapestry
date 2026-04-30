using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Effects;

public class ActiveEffect
{
    public required string Id { get; init; }
    public string? SourceAbilityId { get; init; }
    public Guid SourceEntityId { get; init; }
    public Guid TargetEntityId { get; init; }
    public int RemainingPulses { get; set; }
    public List<StatModifier> StatModifiers { get; init; } = new();
    public List<string> Flags { get; init; } = new();
    public Dictionary<string, object?> Metadata { get; init; } = new();
}
