using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class GameLoopHardeningTests
{
    [Fact]
    public void Tick_processes_system_events_before_commands()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var eventBus = new EventBus();
        var eventQueue = new SystemEventQueue();
        var gameLoop = new GameLoop(
            new CommandRouter(registry, sessions), sessions, eventBus, eventQueue, NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10));

        var order = new List<string>();

        gameLoop.OnSystemEventProcessed += (evt) =>
        {
            order.Add("event");
        };

        // Register a command that records when it runs
        registry.Register("test", (ctx) =>
        {
            order.Add("command");
        });

        // Set up a session with a queued command
        var conn = new FakeConnection();
        var entity = new Entity("player", "TestPlayer");
        var session = new PlayerSession(conn, entity);
        sessions.Add(session);
        conn.SimulateInput("test");

        // Enqueue a system event
        eventQueue.Enqueue(
            new DisconnectEvent(Guid.NewGuid(), Guid.NewGuid(), "test"));

        gameLoop.Tick();

        order.Should().Equal("event", "command");
    }

    [Fact]
    public void Tick_processes_disconnect_event_via_handler()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var eventBus = new EventBus();
        var eventQueue = new SystemEventQueue();
        var gameLoop = new GameLoop(
            new CommandRouter(registry, sessions), sessions, eventBus, eventQueue, NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10));

        DisconnectEvent? received = null;
        gameLoop.OnDisconnect += (evt) =>
        {
            received = evt;
        };

        var sessionId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        eventQueue.Enqueue(new DisconnectEvent(sessionId, entityId, "test reason"));

        gameLoop.Tick();

        received.Should().NotBeNull();
        received!.SessionId.Should().Be(sessionId);
        received.Reason.Should().Be("test reason");
    }

    [Fact]
    public void Tick_ignores_duplicate_disconnect_for_same_entity()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var eventBus = new EventBus();
        var eventQueue = new SystemEventQueue();
        var gameLoop = new GameLoop(
            new CommandRouter(registry, sessions), sessions, eventBus, eventQueue, NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10));

        var callCount = 0;
        gameLoop.OnDisconnect += (evt) =>
        {
            callCount++;
        };

        var entityId = Guid.NewGuid();
        eventQueue.Enqueue(new DisconnectEvent(Guid.NewGuid(), entityId, "first"));
        eventQueue.Enqueue(new DisconnectEvent(Guid.NewGuid(), entityId, "duplicate"));

        gameLoop.Tick();

        callCount.Should().Be(1);
    }

    [Fact]
    public void PlayerSession_tracks_last_input_tick()
    {
        var conn = new FakeConnection();
        var entity = new Entity("player", "TestPlayer");
        var session = new PlayerSession(conn, entity);

        session.LastInputTick.Should().Be(0);
        session.UpdateLastInputTick(42);
        session.LastInputTick.Should().Be(42);
    }

    [Fact]
    public void PlayerSession_tracks_idle_warned_flag()
    {
        var conn = new FakeConnection();
        var entity = new Entity("player", "TestPlayer");
        var session = new PlayerSession(conn, entity);

        session.IdleWarned.Should().BeFalse();
        session.IdleWarned = true;
        session.IdleWarned.Should().BeTrue();
    }

    [Fact]
    public void PlayerSession_EnqueueInput_drops_when_over_limit()
    {
        var conn = new FakeConnection();
        var entity = new Entity("player", "TestPlayer");
        var session = new PlayerSession(conn, entity);

        // Fill to the limit
        for (var i = 0; i < PlayerSession.MaxQueueDepth; i++)
        {
            session.EnqueueInput("command" + i);
        }

        // This one should be dropped
        var accepted = session.EnqueueInput("overflow");

        accepted.Should().BeFalse();
        session.InputQueue.Count.Should().Be(PlayerSession.MaxQueueDepth);
    }

    [Fact]
    public void PlayerSession_EnqueueInput_accepts_when_under_limit()
    {
        var conn = new FakeConnection();
        var entity = new Entity("player", "TestPlayer");
        var session = new PlayerSession(conn, entity);

        var accepted = session.EnqueueInput("hello");

        accepted.Should().BeTrue();
        session.InputQueue.Count.Should().Be(1);
    }

    [Fact]
    public void Disconnect_handler_can_clean_up_entity_from_world()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var eventBus = new EventBus();
        var eventQueue = new SystemEventQueue();
        var gameLoop = new GameLoop(
            new CommandRouter(registry, sessions), sessions, eventBus, eventQueue, NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10));
        var world = new World();

        // Set up a room with a player entity
        var room = new Room("town-square", "Town Square", "A bustling square.");
        world.AddRoom(room);
        var entity = new Entity("player", "TestPlayer");
        room.AddEntity(entity);
        world.TrackEntity(entity);

        // Set up a session
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, entity);
        sessions.Add(session);

        // Wire the disconnect handler (same pattern Program.cs will use)
        gameLoop.OnDisconnect += (evt) =>
        {
            var sess = sessions.GetByEntityId(evt.EntityId);
            if (sess == null)
            {
                return;
            }
            sessions.Remove(sess);
            if (sess.PlayerEntity.LocationRoomId != null)
            {
                var r = world.GetRoom(sess.PlayerEntity.LocationRoomId);
                r?.RemoveEntity(sess.PlayerEntity);
            }
            world.UntrackEntity(sess.PlayerEntity);
        };

        // Enqueue disconnect and tick
        eventQueue.Enqueue(new DisconnectEvent(
            Guid.Parse(conn.Id), entity.Id, "connection closed"));
        gameLoop.Tick();

        // Verify cleanup happened
        sessions.Count.Should().Be(0);
        room.Entities.Should().BeEmpty();
        world.GetEntity(entity.Id).Should().BeNull();
    }
}
