using System.Diagnostics;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;
using Tapestry.Shared;

namespace Tapestry.Engine.Heartbeat;

public class HeartbeatManager
{
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly CombatManager _combatManager;
    private readonly AbilityRegistry _abilityRegistry;
    private readonly ProficiencyManager _proficiencyManager;
    private readonly PassiveAbilityProcessor _passiveAbilityProcessor;
    private readonly EffectManager _effectManager;
    private readonly SessionManager _sessionManager;
    private readonly AlignmentManager _alignmentManager;
    private readonly Random _random;
    private readonly List<IPulseHandler> _handlers = new();
    private IPulseHandler[] _sortedHandlers = [];
    private bool _handlersDirty = true;
    private PulseContext _context = null!;
    private long _tickCount;

    public long TickCount => _tickCount;

    public HeartbeatManager(
        World world,
        EventBus eventBus,
        CombatManager combatManager,
        AbilityRegistry abilityRegistry,
        ProficiencyManager proficiencyManager,
        PassiveAbilityProcessor passiveAbilityProcessor,
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
        _passiveAbilityProcessor = passiveAbilityProcessor;
        _effectManager = effectManager;
        _sessionManager = sessionManager;
        _alignmentManager = alignmentManager;
        _random = random ?? new Random();
        _context = new PulseContext
        {
            World = _world,
            EventBus = _eventBus,
            CombatManager = _combatManager,
            AbilityRegistry = _abilityRegistry,
            ProficiencyManager = _proficiencyManager,
            PassiveAbilityProcessor = _passiveAbilityProcessor,
            EffectManager = _effectManager,
            SessionManager = _sessionManager,
            AlignmentManager = _alignmentManager,
            Random = _random
        };
    }

    public void Register(IPulseHandler handler)
    {
        _handlers.Add(handler);
        _handlersDirty = true;
    }

    public void Tick()
    {
        _tickCount++;

        if (_handlersDirty)
        {
            _sortedHandlers = _handlers.OrderBy(h => h.Priority).ToArray();
            _handlersDirty = false;
        }

        _context.CurrentTick = _tickCount;

        for (var i = 0; i < _sortedHandlers.Length; i++)
        {
            var handler = _sortedHandlers[i];
            if (_tickCount % handler.Cadence != 0) { continue; }

            _context.CurrentPulse = _tickCount / handler.Cadence;

            using var pulseSpan = TapestryTracing.Source.StartActivity($"Pulse.{handler.Name}");
            pulseSpan?.SetTag("pulse.name", handler.Name);
            pulseSpan?.SetTag("pulse.cadence", handler.Cadence);
            handler.Execute(_context);
        }
    }
}
