using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Registration;
using Tapestry.Engine.Rest;
using Tapestry.Engine.Tests.Registration;
using Tapestry.Engine.Stats;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

[CollectionDefinition("GameLoopSerial", DisableParallelization = true)]
public class GameLoopSerialCollection { }

[Collection("GameLoopSerial")]
public class GameLoopTests
{
    [Fact]
    public async Task Tick_ProcessesQueuedCommand()
    {
        var (loop, registry, sessions, world, _) = CreateLoop();
        string? receivedArg = null;

        registry.Register("say", (ctx) =>
        {
            receivedArg = string.Join(" ", ctx.RawArgs);
        }, packName: "test");

        var (session, _) = AddPlayer(sessions, world, "test:room");
        session.EnqueueInput("say hello");

        loop.Tick();

        receivedArg.Should().Be("hello");
    }

    [Fact]
    public async Task Tick_CallsTickHandlers()
    {
        var (loop, _, _, _, policy) = CreateLoop();
        var tickCalled = false;

        loop.RegisterTickHandler("test", 1, () => { tickCalled = true; });
        policy.Resolve();

        loop.Tick();

        tickCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Tick_TickHandler_RespectsInterval()
    {
        var (loop, _, _, _, policy) = CreateLoop();
        var callCount = 0;

        loop.RegisterTickHandler("test", 3, () => { callCount++; });
        policy.Resolve();

        loop.Tick(); // tick 1
        loop.Tick(); // tick 2
        loop.Tick(); // tick 3 — should fire

        callCount.Should().Be(1);
    }

    [Fact]
    public void Tick_RegeneratesVitals()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var eventBus = new EventBus();
        var policy = TestRegistrationPolicy.Create();
        var gameLoop = new GameLoop(new CommandRouter(registry, sessions, world), sessions, eventBus, new SystemEventQueue(), NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10), new NotificationQueue(), policy);

        var entity = new Entity("player", "Test");
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.BaseMaxResource = 50;
        entity.Stats.BaseMaxMovement = 80;
        entity.Stats.Hp = 50;
        entity.Stats.Resource = 20;
        entity.Stats.Movement = 40;
        entity.SetProperty("regen_hp", 5);
        entity.SetProperty("regen_resource", 3);
        entity.SetProperty("regen_movement", 4);

        var vitals = new VitalsService(eventBus);
        gameLoop.RegisterRegenHandler(world, eventBus, regenIntervalTicks: 1, restConfig: null, combatManager: new CombatManager(world, eventBus), vitalsService: vitals);
        policy.Resolve();

        var room = new Room("test", "Test", "Test room");
        room.AddEntity(entity);
        world.AddRoom(room);
        world.TrackEntity(entity);

        var vitalChanges = new List<GameEvent>();
        eventBus.Subscribe("entity.vital.changed", vitalChanges.Add);

        gameLoop.Tick();

        entity.Stats.Hp.Should().Be(55);
        entity.Stats.Resource.Should().Be(23);
        entity.Stats.Movement.Should().Be(44);
        vitalChanges.Should().Contain(e => (string)e.Data["vital"]! == "hp" && (string)e.Data["reason"]! == "regen");
    }

    [Fact]
    public void Tick_DoesNotRegenEntitiesInCombat()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var eventBus = new EventBus();
        var policy = TestRegistrationPolicy.Create();
        var combat = new CombatManager(world, eventBus);
        var gameLoop = new GameLoop(new CommandRouter(registry, sessions, world), sessions, eventBus, new SystemEventQueue(), NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10), new NotificationQueue(), policy);

        var player = new Entity("player", "Test");
        player.AddTag("player");
        player.Stats.BaseMaxHp = 100;
        player.Stats.Hp = 50;
        player.Stats.BaseStrength = 10;
        player.Stats.BaseDexterity = 10;
        player.SetProperty("regen_hp", 5);

        var mob = new Entity("npc", "Foe");
        mob.AddTag("npc");
        mob.Stats.BaseMaxHp = 100;
        mob.Stats.Hp = 50;
        mob.Stats.BaseStrength = 8;
        mob.Stats.BaseDexterity = 10;
        mob.SetProperty("level", 3);
        mob.SetProperty("regen_hp", 5);

        var vitals = new VitalsService(eventBus);
        gameLoop.RegisterRegenHandler(world, eventBus, regenIntervalTicks: 1, restConfig: null, combatManager: combat, vitalsService: vitals);
        policy.Resolve();

        var room = new Room("test", "Test", "Test room");
        room.AddEntity(player);
        room.AddEntity(mob);
        world.AddRoom(room);
        world.TrackEntity(player);
        world.TrackEntity(mob);

        combat.Engage(player, mob);

        gameLoop.Tick();

        // No regen while fighting (ROM "no heal in combat") - the boss's chunk must stick.
        player.Stats.Hp.Should().Be(50);
        mob.Stats.Hp.Should().Be(50);
    }

    [Fact]
    public void Tick_RegenDoesNotExceedMax()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var eventBus = new EventBus();
        var policy = TestRegistrationPolicy.Create();
        var gameLoop = new GameLoop(new CommandRouter(registry, sessions, world), sessions, eventBus, new SystemEventQueue(), NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10), new NotificationQueue(), policy);

        var entity = new Entity("player", "Test");
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.Hp = 98;
        entity.SetProperty("regen_hp", 5);

        var vitals = new VitalsService(eventBus);
        gameLoop.RegisterRegenHandler(world, eventBus, regenIntervalTicks: 1, restConfig: null, combatManager: new CombatManager(world, eventBus), vitalsService: vitals);
        policy.Resolve();

        var room = new Room("test", "Test", "Test room");
        room.AddEntity(entity);
        world.AddRoom(room);
        world.TrackEntity(entity);

        gameLoop.Tick();

        entity.Stats.Hp.Should().Be(100);
    }

    [Fact]
    public void VitalDepleted_FiresEvent()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var eventBus = new EventBus();
        var gameLoop = new GameLoop(new CommandRouter(registry, sessions, world), sessions, eventBus, new SystemEventQueue(), NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10), new NotificationQueue(), TestRegistrationPolicy.Create());

        var entity = new Entity("player", "Test");
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.Hp = 100;

        GameEvent? firedEvent = null;
        eventBus.Subscribe("entity.vital.depleted", evt =>
        {
            firedEvent = evt;
        });

        entity.Stats.Hp = 0;
        gameLoop.CheckVitalDepletion(entity, eventBus);

        firedEvent.Should().NotBeNull();
        firedEvent!.Data["vital"].Should().Be("hp");
        firedEvent.SourceEntityId.Should().Be(entity.Id);
    }

    [Fact]
    public void Tick_SymbolAlias_NoSpace_SplitsAndRoutes()
    {
        var (loop, registry, sessions, world, _) = CreateLoop();
        string? receivedArg = null;

        registry.Register("say", (ctx) =>
        {
            receivedArg = string.Join(" ", ctx.RawArgs);
        }, aliases: ["'"], packName: "test");

        var (session, _) = AddPlayer(sessions, world, "test:room");
        session.EnqueueInput("'hello");

        loop.Tick();

        receivedArg.Should().Be("hello");
    }

    [Fact]
    public void Tick_SymbolAlias_WithSpace_StillRoutes()
    {
        var (loop, registry, sessions, world, _) = CreateLoop();
        string? receivedArg = null;

        registry.Register("say", (ctx) =>
        {
            receivedArg = string.Join(" ", ctx.RawArgs);
        }, aliases: ["'"], packName: "test");

        var (session, _) = AddPlayer(sessions, world, "test:room");
        session.EnqueueInput("' hello");

        loop.Tick();

        receivedArg.Should().Be("hello");
    }

    [Fact]
    public void Tick_SymbolPrefix_NonAlias_DoesNotSplit()
    {
        var (loop, registry, sessions, world, _) = CreateLoop();
        var handled = false;

        registry.Register("look", (ctx) => { handled = true; }, packName: "test");

        var (session, _) = AddPlayer(sessions, world, "test:room");
        session.EnqueueInput("'unknown");

        loop.Tick();

        handled.Should().BeFalse();
    }

    [Fact]
    public void Tick_FirstHandlerThrows_SecondHandlerStillFires()
    {
        var (loop, _, _, _, policy) = CreateLoop();
        var secondFired = false;

        loop.RegisterTickHandler("bad-handler", 1, () =>
        {
            throw new InvalidOperationException("boom");
        });
        loop.RegisterTickHandler("good-handler", 1, () =>
        {
            secondFired = true;
        });
        policy.Resolve();

        loop.Tick();

        secondFired.Should().BeTrue();
    }

    [Fact]
    public void ProcessInput_SemicolonChain_DispatchesBothCommands()
    {
        var (loop, registry, sessions, world, _) = CreateLoop();
        var dispatched = new List<string>();

        registry.Register("north", (ctx) => { dispatched.Add("north"); }, packName: "test");
        registry.Register("east", (ctx) => { dispatched.Add("east"); }, packName: "test");

        var (session, _) = AddPlayer(sessions, world, "test:room");
        session.EnqueueInput("north;east");

        loop.Tick();

        dispatched.Should().HaveCount(2);
        dispatched.Should().Contain("north");
        dispatched.Should().Contain("east");
    }

    [Fact]
    public void RegisterTickHandler_WithIntervalZeroOrNegative_Throws()
    {
        var (loop, _, _, _, _) = CreateLoop();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            loop.RegisterTickHandler("test", 0, () => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            loop.RegisterTickHandler("test", -1, () => { }));
    }

    [Fact]
    public void RegisterTickHandler_SameName_Throws()
    {
        var (loop, _, _, _, policy) = CreateLoop();

        // Strict registration: a duplicate name is always a bug (e.g. two subsystems both
        // picking "heartbeat"). Registration is now declarative -- both records are accepted,
        // and the collision is raised at the seal (Resolve()), not eagerly at register time.
        loop.RegisterTickHandler("my-handler", 1, () => { });
        loop.RegisterTickHandler("my-handler", 1, () => { });

        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }

    [Fact]
    public void RegisterTickHandler_CancelThenReRegister_ReplacesHandler()
    {
        var (loop, _, _, _, policy) = CreateLoop();
        var firstCount = 0;
        var secondCount = 0;

        // The sanctioned way to swap a handler today: cancel, then register the replacement.
        // Pre-seal, the cancel pulls the first candidate from the ledger so no collision is raised.
        loop.RegisterTickHandler("my-handler", 1, () => { firstCount++; });
        loop.CancelTickHandler("my-handler");
        loop.RegisterTickHandler("my-handler", 1, () => { secondCount++; });
        policy.Resolve();

        loop.Tick();

        firstCount.Should().Be(0);
        secondCount.Should().Be(1);
    }

    [Fact]
    public void CancelTickHandler_PreventsSubsequentFiring()
    {
        var (loop, _, _, _, policy) = CreateLoop();
        var callCount = 0;

        loop.RegisterTickHandler("cancellable", 1, () => { callCount++; });
        loop.CancelTickHandler("cancellable");
        policy.Resolve();

        loop.Tick();

        callCount.Should().Be(0);
    }

    [Fact]
    public void RegisterTickHandler_DueOrdered_FiresAtRegistrationPlusInterval()
    {
        var (loop, _, _, _, policy) = CreateLoop();
        var firedOnTick = -1L;

        // Register when tickCount = 0; interval = 3; should fire on tick 3.
        loop.RegisterTickHandler("test", 3, () => { firedOnTick = loop.TickCount; });
        policy.Resolve();

        loop.Tick(); // tick 1
        loop.Tick(); // tick 2
        loop.Tick(); // tick 3 — fires

        firedOnTick.Should().Be(3);
    }

    [Fact]
    public void RegisterTickHandler_DueOrdered_KeepsCorrectCadenceAfterFirstFire()
    {
        var (loop, _, _, _, policy) = CreateLoop();
        var fireTicks = new List<long>();

        loop.RegisterTickHandler("test", 3, () => { fireTicks.Add(loop.TickCount); });
        policy.Resolve();

        for (var i = 0; i < 9; i++) { loop.Tick(); }

        fireTicks.Should().Equal(new[] { 3L, 6L, 9L });
    }

    [Fact]
    public void RegisterRegenHandler_EmitsRegenEvent_ForMovement()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var eventBus = new EventBus();
        var policy = TestRegistrationPolicy.Create();
        var loop = new GameLoop(new CommandRouter(registry, sessions, world), sessions, eventBus,
            new SystemEventQueue(), NullLogger<GameLoop>.Instance, new TapestryMetrics(),
            new TickTimer(10), new NotificationQueue(), policy);

        var entity = new Entity("player", "Test");
        entity.Stats.BaseMaxMovement = 100;
        entity.Stats.Movement = 50;
        entity.SetProperty("regen_movement", 10);

        var vitals = new VitalsService(eventBus);
        loop.RegisterRegenHandler(world, eventBus, regenIntervalTicks: 1, restConfig: null, combatManager: new CombatManager(world, eventBus), vitalsService: vitals);
        policy.Resolve();

        var room = new Room("test", "Test", "Test room");
        room.AddEntity(entity);
        world.AddRoom(room);
        world.TrackEntity(entity);

        GameEvent? capturedEvent = null;
        eventBus.Subscribe("entity.regen", evt =>
        {
            if ((string?)evt.Data.GetValueOrDefault("vital") == "movement")
            {
                capturedEvent = evt;
            }
        });

        loop.Tick();

        capturedEvent.Should().NotBeNull();
        capturedEvent!.Data["vital"].Should().Be("movement");
        capturedEvent.Data["amount"].Should().Be(10);
    }

    [Fact]
    public void RegisterRegenHandler_MovementRegenEvent_IsRespectedWhenMutated()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var eventBus = new EventBus();
        var policy = TestRegistrationPolicy.Create();
        var loop = new GameLoop(new CommandRouter(registry, sessions, world), sessions, eventBus,
            new SystemEventQueue(), NullLogger<GameLoop>.Instance, new TapestryMetrics(),
            new TickTimer(10), new NotificationQueue(), policy);

        var entity = new Entity("player", "Test");
        entity.Stats.BaseMaxMovement = 100;
        entity.Stats.Movement = 50;
        entity.SetProperty("regen_movement", 10);

        var vitals = new VitalsService(eventBus);
        loop.RegisterRegenHandler(world, eventBus, regenIntervalTicks: 1, restConfig: null, combatManager: new CombatManager(world, eventBus), vitalsService: vitals);
        policy.Resolve();

        var room = new Room("test", "Test", "Test room");
        room.AddEntity(entity);
        world.AddRoom(room);
        world.TrackEntity(entity);

        // Subscriber cancels movement regen
        eventBus.Subscribe("entity.regen", evt =>
        {
            if ((string?)evt.Data.GetValueOrDefault("vital") == "movement")
            {
                evt.Cancelled = true;
            }
        });

        loop.Tick();

        entity.Stats.Movement.Should().Be(50); // unchanged because event was cancelled
    }

    [Fact]
    public void RegisterRegenHandler_WithNoSustenanceConfig_RegensFully()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var eventBus = new EventBus();
        var policy = TestRegistrationPolicy.Create();
        var loop = new GameLoop(new CommandRouter(registry, sessions, world), sessions, eventBus,
            new SystemEventQueue(), NullLogger<GameLoop>.Instance, new TapestryMetrics(),
            new TickTimer(10), new NotificationQueue(), policy);

        var entity = new Entity("player", "Test");
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.Hp = 50;
        entity.SetProperty("regen_hp", 5);
        entity.SetProperty("sustenance", 0); // famished — would have suppressed regen before

        var vitals = new VitalsService(eventBus);
        loop.RegisterRegenHandler(world, eventBus, regenIntervalTicks: 1, restConfig: null, combatManager: new CombatManager(world, eventBus), vitalsService: vitals);
        policy.Resolve();

        var room = new Room("test", "Test", "Test room");
        world.AddRoom(room);
        entity.LocationRoomId = "test";
        world.TrackEntity(entity);

        loop.Tick();

        entity.Stats.Hp.Should().Be(55); // full regen regardless of sustenance
    }

    [Fact]
    public void RegisterRegenHandler_RoomRestBonus_ContributesToRegenMultiplier()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var eventBus = new EventBus();
        var policy = TestRegistrationPolicy.Create();
        var loop = new GameLoop(new CommandRouter(registry, sessions, world), sessions, eventBus,
            new SystemEventQueue(), NullLogger<GameLoop>.Instance, new TapestryMetrics(),
            new TickTimer(10), new NotificationQueue(), policy);

        var entity = new Entity("player", "Test");
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.Hp = 50;
        entity.SetProperty("regen_hp", 5);
        entity.SetProperty(RestProperties.RestState, RestProperties.StateResting);

        var restConfig = new RestConfig();
        var vitals = new VitalsService(eventBus);
        loop.RegisterRegenHandler(world, eventBus, regenIntervalTicks: 1, restConfig: restConfig, combatManager: new CombatManager(world, eventBus), vitalsService: vitals);
        policy.Resolve();

        var room = new Room("test", "Test", "Test room");
        room.SetProperty(RestProperties.RestBonus, 1);
        room.AddEntity(entity);
        world.AddRoom(room);
        world.TrackEntity(entity);

        loop.Tick();

        // restMultiplier = RestingMultiplier (2.0) + room rest_bonus (1) = 3.0
        // regen_hp 5 * 3.0 = 15 -> Hp = 50 + 15 = 65
        entity.Stats.Hp.Should().Be(65);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    [Fact]
    public void Tick_CapturesHandlerCpu_DistinguishesSleepFromSpin()
    {
        ThreadCpuClock.IsSupported.Should().BeTrue("dev/CI must support the CPU clock");

        var logger = new CapturingLogger<GameLoop>();
        var sessions = new SessionManager();
        var world = new World();
        var bus = new EventBus();
        var policy = TestRegistrationPolicy.Create();
        var loop = new GameLoop(new CommandRouter(new CommandRegistry(), sessions, world),
            sessions, bus, new SystemEventQueue(), logger, new TapestryMetrics(),
            new TickTimer(10), new NotificationQueue(), policy);
        loop.ConfigureSlowTickThreshold(50);

        var cpuByHandler = new Dictionary<string, double>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == TapestryTracing.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = a =>
            {
                if (!a.OperationName.StartsWith("TickHandler.")) { return; }
                var tag = a.GetTagItem("handler.cpu_ms");
                if (tag != null) { cpuByHandler[a.OperationName] = Convert.ToDouble(tag); }
            }
        };
        ActivitySource.AddActivityListener(listener);

        loop.RegisterTickHandler("sleepy", 1, () => Thread.Sleep(80));
        loop.RegisterTickHandler("spin", 1, () =>
        {
            var cpuStart = ThreadCpuClock.GetCurrentThreadCpuTime();
            var sw = Stopwatch.StartNew();
            // Spin until the handler clearly blows the 50ms wall budget AND has burned real
            // CPU -- so the slow-tick WARN fires on wall, and the cpu classification is cpu-bound.
            while (sw.Elapsed.TotalMilliseconds < 70 ||
                   (ThreadCpuClock.GetCurrentThreadCpuTime() - cpuStart).TotalMilliseconds < 55)
            {
            }
        });
        policy.Resolve();

        loop.Tick();

        cpuByHandler["TickHandler.sleepy"].Should().BeLessThan(30);
        cpuByHandler["TickHandler.spin"].Should().BeGreaterThan(40);

        logger.Messages.Should().Contain(m =>
            m.Contains("Slow tick handler: sleepy ") && m.Contains("(preempted)") &&
            m.Contains("ms wall /") && m.Contains("ms cpu"));
        logger.Messages.Should().Contain(m =>
            m.Contains("Slow tick handler: spin ") && m.Contains("(cpu-bound)") &&
            m.Contains("ms wall /") && m.Contains("ms cpu"));
    }

    [Fact]
    public void Tick_RecordsHandlerWallAndCpuMetrics()
    {
        var sessions = new SessionManager();
        var world = new World();
        var bus = new EventBus();
        var policy = TestRegistrationPolicy.Create();
        var loop = new GameLoop(new CommandRouter(new CommandRegistry(), sessions, world),
            sessions, bus, new SystemEventQueue(), NullLogger<GameLoop>.Instance, new TapestryMetrics(),
            new TickTimer(10), new NotificationQueue(), policy);

        var recorded = new List<(string instrument, string? handler)>();
        using var ml = new MeterListener();
        ml.InstrumentPublished = (inst, l) =>
        {
            if (inst.Meter.Name == TapestryMetrics.MeterName) { l.EnableMeasurementEvents(inst); }
        };
        ml.SetMeasurementEventCallback<double>((inst, value, tags, state) =>
        {
            string? handler = null;
            foreach (var t in tags)
            {
                if (t.Key == "handler") { handler = t.Value?.ToString(); }
            }
            recorded.Add((inst.Name, handler));
        });
        ml.Start();

        loop.RegisterTickHandler("metered", 1, () => { });
        policy.Resolve();
        loop.Tick();

        recorded.Should().Contain(r => r.instrument == "tapestry.tick.handler_wall_ms" && r.handler == "metered");
        recorded.Should().Contain(r => r.instrument == "tapestry.tick.handler_cpu_ms" && r.handler == "metered");
    }

    private static (GameLoop, CommandRegistry, SessionManager, World, RegistrationPolicy) CreateLoop()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var router = new CommandRouter(registry, sessions, world);
        var bus = new EventBus();
        var policy = TestRegistrationPolicy.Create();
        var loop = new GameLoop(router, sessions, bus, new SystemEventQueue(), NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10), new NotificationQueue(), policy);
        return (loop, registry, sessions, world, policy);
    }

    private static (PlayerSession, Entity) AddPlayer(SessionManager sessions, World world, string roomId)
    {
        var room = world.GetRoom(roomId);
        if (room == null)
        {
            room = new Room(roomId, "Test Room", "A test room.");
            world.AddRoom(room);
        }

        var conn = new FakeConnection();
        var entity = new Entity("player", "TestPlayer");
        room.AddEntity(entity);
        var session = new PlayerSession(conn, entity);
        sessions.Add(session);
        return (session, entity);
    }
}