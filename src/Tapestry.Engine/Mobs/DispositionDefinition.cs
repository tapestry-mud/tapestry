namespace Tapestry.Engine.Mobs;

public class DispositionDefinition
{
    public string Default { get; init; } = "neutral";
    public List<DispositionRule> Rules { get; init; } = new();
}

public class DispositionRule
{
    public DispositionCondition When { get; init; } = new();
    public required string Reaction { get; init; }
}

public class DispositionCondition
{
    public int? MinAlignment { get; init; }
    public int? MaxAlignment { get; init; }
    public List<string>? Buckets { get; init; }
    public string? HasTag { get; init; }
}
