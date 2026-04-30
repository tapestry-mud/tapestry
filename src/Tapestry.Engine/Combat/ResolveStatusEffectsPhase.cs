using Tapestry.Engine.Heartbeat;

namespace Tapestry.Engine.Combat;

public class ResolveStatusEffectsPhase : ICombatPhase
{
    public string Name => "ResolveStatusEffects";
    public int Priority => 300;

    public void Execute(PulseContext context)
    {
        context.EffectManager.TickPulse();
    }
}
