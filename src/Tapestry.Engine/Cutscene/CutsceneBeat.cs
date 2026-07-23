namespace Tapestry.Engine.Cutscene;

/// <summary>
/// One authored line of a cutscene (output-cadence-cutscenes spec, section 3, Layer 2).
/// <paramref name="PauseAfterTicks"/> is the delay, in engine ticks (100ms each, matching the
/// swell clock's dials -- e.g. SwellClockManager's DecelGap), before the NEXT beat emits. The
/// last beat's pause is never consulted (the sequence completes the instant it is shown).
/// </summary>
public record CutsceneBeat(string Text, int PauseAfterTicks);
