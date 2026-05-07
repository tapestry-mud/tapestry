using Tapestry.Engine.Abilities;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;

namespace Tapestry.Engine.Heartbeat;

public class PulseContext
{
    public long CurrentTick { get; init; }
    public long CurrentPulse { get; init; }
    public World World { get; init; } = null!;
    public EventBus EventBus { get; init; } = null!;
    public CombatManager CombatManager { get; init; } = null!;
    public AbilityRegistry AbilityRegistry { get; init; } = null!;
    public ProficiencyManager ProficiencyManager { get; init; } = null!;
    public PassiveAbilityProcessor PassiveAbilityProcessor { get; init; } = null!;
    public EffectManager EffectManager { get; init; } = null!;
    public SessionManager SessionManager { get; init; } = null!;
    public AlignmentManager AlignmentManager { get; init; } = null!;
    public Random Random { get; init; } = new();
}
