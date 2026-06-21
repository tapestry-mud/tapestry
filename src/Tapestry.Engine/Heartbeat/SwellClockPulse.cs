using Tapestry.Engine.Combat;

namespace Tapestry.Engine.Heartbeat;

/// <summary>
/// Advances the per-fight swell clock every tick (100ms). Runs before CombatPulse (priority 90 vs 100)
/// so a swell entered this tick suppresses this tick's auto-attack via ResolveAutoAttacksPhase's gate.
/// </summary>
public class SwellClockPulse : IPulseHandler
{
    private readonly SwellClockManager _swell;

    public SwellClockPulse(SwellClockManager swell)
    {
        _swell = swell;
    }

    public string Name => "SwellClockPulse";
    public int Cadence => 1;
    public int Priority => 90;

    public void Execute(PulseContext context)
    {
        _swell.AdvanceAll(context.CurrentTick);
    }
}
