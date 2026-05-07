using Tapestry.Engine.Abilities;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Heartbeat;

namespace Tapestry.Engine.Tests.Heartbeat;

public class HeartbeatManagerTests
{
    [Fact]
    public void PulseContext_Exposes_CurrentTick_And_CurrentPulse()
    {
        var ctx = new PulseContext
        {
            CurrentTick = 100,
            CurrentPulse = 5
        };
        Assert.Equal(100, ctx.CurrentTick);
        Assert.Equal(5, ctx.CurrentPulse);
    }
}

public class HeartbeatManager_TickTests
{
    private HeartbeatManager _heartbeat = null!;
    private int _handlerCallCount;
    private PulseContext? _lastContext;

    private static HeartbeatManager CreateHeartbeatManager(Random? random = null)
    {
        var world = new World();
        var eventBus = new EventBus();
        var combatManager = new CombatManager(world, eventBus);
        var abilityRegistry = new AbilityRegistry();
        var proficiencyManager = new ProficiencyManager(world, abilityRegistry);
        var effectManager = new EffectManager(world, eventBus);
        var sessionManager = new SessionManager();
        var alignmentManager = new AlignmentManager(world, eventBus, new AlignmentConfig());
        return new HeartbeatManager(world, eventBus, combatManager, abilityRegistry,
            proficiencyManager, effectManager, sessionManager, alignmentManager, random);
    }

    private void Setup()
    {
        _heartbeat = CreateHeartbeatManager();
        _handlerCallCount = 0;
        _lastContext = null;
    }

    [Fact]
    public void Tick_FiresHandler_WhenCadenceDue()
    {
        Setup();
        _heartbeat.Register(new TestPulseHandler(
            cadence: 5, priority: 100, onExecute: ctx =>
            {
                _handlerCallCount++;
                _lastContext = ctx;
            }));

        for (var i = 0; i < 4; i++)
        {
            _heartbeat.Tick();
        }
        Assert.Equal(0, _handlerCallCount);

        _heartbeat.Tick();
        Assert.Equal(1, _handlerCallCount);
        Assert.NotNull(_lastContext);
        Assert.Equal(5, _lastContext!.CurrentTick);
        Assert.Equal(1, _lastContext.CurrentPulse);
    }

    [Fact]
    public void Tick_FiresMultipleHandlers_InPriorityOrder()
    {
        Setup();
        var order = new List<string>();
        _heartbeat.Register(new TestPulseHandler("B", cadence: 5, priority: 200, onExecute: _ => order.Add("B")));
        _heartbeat.Register(new TestPulseHandler("A", cadence: 5, priority: 100, onExecute: _ => order.Add("A")));

        for (var i = 0; i < 5; i++)
        {
            _heartbeat.Tick();
        }

        Assert.Equal(new[] { "A", "B" }, order);
    }

    [Fact]
    public void Tick_DifferentCadences_FireIndependently()
    {
        Setup();
        var fastCount = 0;
        var slowCount = 0;
        _heartbeat.Register(new TestPulseHandler("fast", cadence: 2, priority: 100, onExecute: _ => fastCount++));
        _heartbeat.Register(new TestPulseHandler("slow", cadence: 5, priority: 100, onExecute: _ => slowCount++));

        for (var i = 0; i < 10; i++)
        {
            _heartbeat.Tick();
        }

        Assert.Equal(5, fastCount);
        Assert.Equal(2, slowCount);
    }

    [Fact]
    public void TickCount_Increments_EachTick()
    {
        Setup();
        Assert.Equal(0, _heartbeat.TickCount);
        _heartbeat.Tick();
        Assert.Equal(1, _heartbeat.TickCount);
        _heartbeat.Tick();
        Assert.Equal(2, _heartbeat.TickCount);
    }

    [Fact]
    public void Tick_PulseContext_ContainsAlignmentManager()
    {
        var world = new World();
        var eventBus = new EventBus();
        var combatManager = new CombatManager(world, eventBus);
        var abilityRegistry = new AbilityRegistry();
        var proficiencyManager = new ProficiencyManager(world, abilityRegistry);
        var effectManager = new EffectManager(world, eventBus);
        var sessionManager = new SessionManager();
        var alignmentManager = new AlignmentManager(world, eventBus, new AlignmentConfig());
        var heartbeat = new HeartbeatManager(world, eventBus, combatManager, abilityRegistry,
            proficiencyManager, effectManager, sessionManager, alignmentManager);

        PulseContext? captured = null;
        heartbeat.Register(new TestPulseHandler(cadence: 1, onExecute: ctx => { captured = ctx; }));
        heartbeat.Tick();

        Assert.NotNull(captured);
        Assert.Same(alignmentManager, captured!.AlignmentManager);
    }

    [Fact]
    public void Tick_PulseContext_UsesInjectedRandom()
    {
        var deterministicRandom = new Random(42);
        var heartbeat = CreateHeartbeatManager(random: deterministicRandom);

        PulseContext? captured = null;
        heartbeat.Register(new TestPulseHandler(cadence: 1, onExecute: ctx => { captured = ctx; }));
        heartbeat.Tick();

        Assert.NotNull(captured);
        Assert.Same(deterministicRandom, captured!.Random);
    }
}

internal class TestPulseHandler : IPulseHandler
{
    private readonly Action<PulseContext> _onExecute;

    public string Name { get; }
    public int Cadence { get; }
    public int Priority { get; }

    public TestPulseHandler(string name = "test", int cadence = 1, int priority = 100,
        Action<PulseContext>? onExecute = null)
    {
        Name = name;
        Cadence = cadence;
        Priority = priority;
        _onExecute = onExecute ?? (_ => { });
    }

    public void Execute(PulseContext context)
    {
        _onExecute(context);
    }
}
