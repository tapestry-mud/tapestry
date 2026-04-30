using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Heartbeat;

namespace Tapestry.Engine.Tests.Heartbeat;

public class CombatPulseTests
{
    [Fact]
    public void CombatPulse_HasCadence20()
    {
        var pulse = new CombatPulse(
            new AbilityResolutionPhase(),
            new List<ICombatPhase>());

        Assert.Equal(20, pulse.Cadence);
    }

    [Fact]
    public void Execute_RunsAbilityPhaseFirst_ThenCombatPhasesInPriorityOrder()
    {
        var order = new List<string>();
        var phases = new List<ICombatPhase>
        {
            new TrackingCombatPhase("wimpy", 400, order),
            new TrackingCombatPhase("auto-attacks", 200, order),
            new TrackingCombatPhase("status-effects", 300, order)
        };

        var abilityPhase = new TrackingAbilityPhase(order);
        var pulse = new CombatPulse(abilityPhase, phases);

        var world = new World();
        var eventBus = new EventBus();
        var registry = new AbilityRegistry();
        var effectManager = new EffectManager(world, eventBus);

        pulse.Execute(new PulseContext
        {
            CurrentTick = 20,
            CurrentPulse = 1,
            World = world,
            EventBus = eventBus,
            CombatManager = new CombatManager(world, eventBus, effectManager: effectManager),
            AbilityRegistry = registry,
            ProficiencyManager = new ProficiencyManager(world, registry),
            EffectManager = effectManager,
            SessionManager = new SessionManager()
        });

        Assert.Equal(new[] { "abilities", "auto-attacks", "status-effects", "wimpy" }, order);
    }
}

internal class TrackingCombatPhase : ICombatPhase
{
    private readonly List<string> _order;

    public string Name { get; }
    public int Priority { get; }

    public TrackingCombatPhase(string name, int priority, List<string> order)
    {
        Name = name;
        Priority = priority;
        _order = order;
    }

    public void Execute(PulseContext context)
    {
        _order.Add(Name);
    }
}

internal class TrackingAbilityPhase : IPulseHandler
{
    private readonly List<string> _order;

    public string Name => "TrackingAbilityPhase";
    public int Cadence => 1;
    public int Priority => 100;

    public TrackingAbilityPhase(List<string> order)
    {
        _order = order;
    }

    public void Execute(PulseContext context)
    {
        _order.Add("abilities");
    }
}
