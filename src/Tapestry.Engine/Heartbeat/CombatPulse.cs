using System.Diagnostics;
using Tapestry.Engine.Combat;

namespace Tapestry.Engine.Heartbeat;

public class CombatPulse : IPulseHandler
{
    private readonly IPulseHandler _abilityPhase;
    private readonly List<ICombatPhase> _combatPhases;

    public string Name => "CombatPulse";
    public int Cadence => 20;
    public int Priority => 100;

    public CombatPulse(IPulseHandler abilityPhase, List<ICombatPhase> combatPhases)
    {
        _abilityPhase = abilityPhase;
        _combatPhases = combatPhases;
    }

    public void Execute(PulseContext context)
    {
        using (TapestryTracing.Source.StartActivity("CombatPulse.AbilityResolution"))
        {
            _abilityPhase.Execute(context);
        }

        foreach (var phase in _combatPhases.OrderBy(p => p.Priority))
        {
            using var phaseSpan = TapestryTracing.Source.StartActivity($"CombatPulse.{phase.GetType().Name}");
            phase.Execute(context);
        }
    }
}
