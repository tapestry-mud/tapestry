using System.Diagnostics;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;

namespace Tapestry.Engine.Heartbeat;

public class HeartbeatManager
{
    private readonly List<IPulseHandler> _handlers = new();
    private long _tickCount;

    public long TickCount => _tickCount;

    public World? World { get; set; }
    public EventBus? EventBus { get; set; }
    public CombatManager? CombatManager { get; set; }
    public AbilityRegistry? AbilityRegistry { get; set; }
    public ProficiencyManager? ProficiencyManager { get; set; }
    public EffectManager? EffectManager { get; set; }
    public SessionManager? SessionManager { get; set; }
    public AlignmentManager? AlignmentManager { get; set; }
    public Random Random { get; set; } = new();

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
                World = World!,
                EventBus = EventBus!,
                CombatManager = CombatManager!,
                AbilityRegistry = AbilityRegistry!,
                ProficiencyManager = ProficiencyManager!,
                EffectManager = EffectManager!,
                SessionManager = SessionManager!,
                AlignmentManager = AlignmentManager!,
                Random = Random
            };

            using var pulseSpan = TapestryTracing.Source.StartActivity($"Pulse.{handler.Name}");
            pulseSpan?.SetTag("pulse.name", handler.Name);
            pulseSpan?.SetTag("pulse.cadence", handler.Cadence);
            handler.Execute(context);
        }
    }
}
