using System.Diagnostics;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;

namespace Tapestry.Engine.Heartbeat;

public class HeartbeatManager
{
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly CombatManager _combatManager;
    private readonly AbilityRegistry _abilityRegistry;
    private readonly ProficiencyManager _proficiencyManager;
    private readonly EffectManager _effectManager;
    private readonly SessionManager _sessionManager;
    private readonly AlignmentManager _alignmentManager;
    private readonly Random _random;
    private readonly List<IPulseHandler> _handlers = new();
    private long _tickCount;

    public long TickCount => _tickCount;

    public HeartbeatManager(
        World world,
        EventBus eventBus,
        CombatManager combatManager,
        AbilityRegistry abilityRegistry,
        ProficiencyManager proficiencyManager,
        EffectManager effectManager,
        SessionManager sessionManager,
        AlignmentManager alignmentManager,
        Random? random = null)
    {
        _world = world;
        _eventBus = eventBus;
        _combatManager = combatManager;
        _abilityRegistry = abilityRegistry;
        _proficiencyManager = proficiencyManager;
        _effectManager = effectManager;
        _sessionManager = sessionManager;
        _alignmentManager = alignmentManager;
        _random = random ?? new Random();
    }

    public void Register(IPulseHandler handler)
    {
        _handlers.Add(handler);
    }

    public void Tick()
    {
        _tickCount++;

        var dueHandlers = _handlers
            .Where(h => _tickCount % h.Cadence == 0)
            .OrderBy(h => h.Priority);

        foreach (var handler in dueHandlers)
        {
            var context = new PulseContext
            {
                CurrentTick = _tickCount,
                CurrentPulse = _tickCount / handler.Cadence,
                World = _world,
                EventBus = _eventBus,
                CombatManager = _combatManager,
                AbilityRegistry = _abilityRegistry,
                ProficiencyManager = _proficiencyManager,
                EffectManager = _effectManager,
                SessionManager = _sessionManager,
                AlignmentManager = _alignmentManager,
                Random = _random
            };

            using var pulseSpan = TapestryTracing.Source.StartActivity($"Pulse.{handler.Name}");
            pulseSpan?.SetTag("pulse.name", handler.Name);
            pulseSpan?.SetTag("pulse.cadence", handler.Cadence);
            handler.Execute(context);
        }
    }
}
