namespace Tapestry.Engine.Combat;

public enum SwellPhase
{
    Baseline,
    Telegraph,
    Window,
    Resolve
}

public enum SwellCommitResult
{
    Accepted,
    NoSwellHere,
    NotInWindow,
    AlreadyCommitted
}

/// <summary>Per-fight swell state, keyed by the boss entity id. Solo slice: one player target.</summary>
internal sealed class SwellState
{
    public SwellPhase Phase { get; set; } = SwellPhase.Baseline;
    public long PhaseEndsAtTick { get; set; }
    public Guid PlayerId { get; set; }
    public string AttackLine { get; set; } = "";
    public string RequiredCounter { get; set; } = "";
    public string Tell { get; set; } = "full";
    public string Mode { get; set; } = "stretch";
    public string? CommittedVerb { get; set; }
    // Stretch deceleration: the wind-up emits a few cadence beats with growing gaps across
    // the telegraph budget (the playtest-winning feel). DecelIdx counts beats emitted;
    // DecelNextTick is when the next cadence line is due.
    public int DecelIdx { get; set; }
    public long DecelNextTick { get; set; }
}
