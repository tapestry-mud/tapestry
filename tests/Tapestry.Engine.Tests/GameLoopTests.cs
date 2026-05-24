using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Stats;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class GameLoopTests
{
    [Fact]
    public async Task Tick_ProcessesQueuedCommand()
    {
        var (loop, registry, sessions, world) = CreateLoop();
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
        var (loop, _, _, _) = CreateLoop();
        var tickCalled = false;

        loop.RegisterTickHandler("test", 1, () => { tickCalled = true; });

        loop.Tick();

        tickCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Tick_TickHandler_RespectsInterval()
    {
        var (loop, _, _, _) = CreateLoop();
        var callCount = 0;

        loop.RegisterTickHandler("test", 3, () => { callCount++; });

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
        var gameLoop = new GameLoop(new CommandRouter(registry, sessions, world), sessions, eventBus, new SystemEventQueue(), NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10), new NotificationQueue());

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

        gameLoop.RegisterRegenHandler(world, eventBus, regenIntervalTicks: 1);

        var room = new Room("test", "Test", "Test room");
        room.AddEntity(entity);
        world.AddRoom(room);
        world.TrackEntity(entity);

        gameLoop.Tick();

        entity.Stats.Hp.Should().Be(55);
        entity.Stats.Resource.Should().Be(23);
        entity.Stats.Movement.Should().Be(44);
    }

    [Fact]
    public void Tick_RegenDoesNotExceedMax()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var eventBus = new EventBus();
        var gameLoop = new GameLoop(new CommandRouter(registry, sessions, world), sessions, eventBus, new SystemEventQueue(), NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10), new NotificationQueue());

        var entity = new Entity("player", "Test");
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.Hp = 98;
        entity.SetProperty("regen_hp", 5);

        gameLoop.RegisterRegenHandler(world, eventBus, regenIntervalTicks: 1);

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
        var gameLoop = new GameLoop(new CommandRouter(registry, sessions, world), sessions, eventBus, new SystemEventQueue(), NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10), new NotificationQueue());

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
        var (loop, registry, sessions, world) = CreateLoop();
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
        var (loop, registry, sessions, world) = CreateLoop();
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
        var (loop, registry, sessions, world) = CreateLoop();
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
        var (loop, _, _, _) = CreateLoop();
        var secondFired = false;

        loop.RegisterTickHandler("bad-handler", 1, () =>
        {
            throw new InvalidOperationException("boom");
        });
        loop.RegisterTickHandler("good-handler", 1, () =>
        {
            secondFired = true;
        });

        loop.Tick();

        secondFired.Should().BeTrue();
    }

    [Fact]
    public void ProcessInput_SemicolonChain_DispatchesBothCommands()
    {
        var (loop, registry, sessions, world) = CreateLoop();
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
        var (loop, _, _, _) = CreateLoop();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            loop.RegisterTickHandler("test", 0, () => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            loop.RegisterTickHandler("test", -1, () => { }));
    }

    [Fact]
    public void RegisterTickHandler_SameName_ReplacesExistingHandler()
    {
        var (loop, _, _, _) = CreateLoop();
        var firstCount = 0;
        var secondCount = 0;

        loop.RegisterTickHandler("my-handler", 1, () => { firstCount++; });
        loop.RegisterTickHandler("my-handler", 1, () => { secondCount++; }); // replaces

        loop.Tick();

        firstCount.Should().Be(0);
        secondCount.Should().Be(1);
    }

    [Fact]
    public void CancelTickHandler_PreventsSubsequentFiring()
    {
        var (loop, _, _, _) = CreateLoop();
        var callCount = 0;

        loop.RegisterTickHandler("cancellable", 1, () => { callCount++; });
        loop.CancelTickHandler("cancellable");

        loop.Tick();

        callCount.Should().Be(0);
    }

    [Fact]
    public void RegisterTickHandler_DueOrdered_FiresAtRegistrationPlusInterval()
    {
        var (loop, _, _, _) = CreateLoop();
        var firedOnTick = -1L;

        // Register when tickCount = 0; interval = 3; should fire on tick 3.
        loop.RegisterTickHandler("test", 3, () => { firedOnTick = loop.TickCount; });

        loop.Tick(); // tick 1
        loop.Tick(); // tick 2
        loop.Tick(); // tick 3 — fires

        firedOnTick.Should().Be(3);
    }

    [Fact]
    public void RegisterTickHandler_DueOrdered_KeepsCorrectCadenceAfterFirstFire()
    {
        var (loop, _, _, _) = CreateLoop();
        var fireTicks = new List<long>();

        loop.RegisterTickHandler("test", 3, () => { fireTicks.Add(loop.TickCount); });

        for (var i = 0; i < 9; i++) { loop.Tick(); }

        fireTicks.Should().Equal(new[] { 3L, 6L, 9L });
    }

    private static (GameLoop, CommandRegistry, SessionManager, World) CreateLoop()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var router = new CommandRouter(registry, sessions, world);
        var bus = new EventBus();
        var loop = new GameLoop(router, sessions, bus, new SystemEventQueue(), NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10), new NotificationQueue());
        return (loop, registry, sessions, world);
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