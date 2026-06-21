namespace Tapestry.Engine.Combat;

/// <summary>The deterministic outcome of a swell window. No numbers - magnitudes live on the boss dials.</summary>
public enum WindowOutcome
{
    Countered,
    Whiffed,
    Weathered
}

/// <summary>A read-only view of a combatant for the validator. Diegetic tier, never raw HP.</summary>
public sealed record ActorView(Guid Id, string HpTier);

/// <summary>The live swell, present during Telegraph/Window/Resolve.</summary>
public sealed record SwellView(string AttackLine, string RequiredCounter, string Tell, bool WindowOpen);

/// <summary>The committed command. Verb is empty string when nothing was committed (a weather).</summary>
public sealed record CommandView(string Verb, string? Target);

/// <summary>
/// Read-only snapshot the engine builds from live state and hands to a window validator.
/// Open for growth (momentum, exposures, detectedTypes, combatants, relicCharge) - none built in slice 1.
/// </summary>
public sealed class CombatContext
{
    public required ActorView Actor { get; init; }
    public required ActorView Target { get; init; }
    public required string Phase { get; init; }
    public SwellView? Swell { get; init; }
    public required CommandView Command { get; init; }
}

/// <summary>What a validator returns. Semantic, no numbers. The single decision point.</summary>
public sealed class ValidationResult
{
    public required WindowOutcome Outcome { get; init; }
    public string? Quality { get; init; }
    public required string NarrationKey { get; init; }
}
