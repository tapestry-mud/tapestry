using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Login;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

// Covers the testable half of the half-open-connection fix: the heartbeat sweep and the
// AFK idle-kick. The raw TCP behaviour (TCP_USER_TIMEOUT, a real socket write erroring)
// can only be verified live on the droplet -- here we verify that the engine drives the
// connection's existing write-error -> Disconnect path, and that the AFK kick force-closes
// off LastInputTick rather than trusting the stale IsConnected flag.
public class HalfOpenDetectionTests
{
    private static GameLoop NewGameLoop(SessionManager sessions, SystemEventQueue eventQueue,
        int ticksPerSecond = 10)
    {
        var registry = new CommandRegistry();
        var world = new World();
        return new GameLoop(
            new CommandRouter(registry, sessions, world), sessions, new EventBus(), eventQueue,
            NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(ticksPerSecond),
            new NotificationQueue());
    }

    private static PlayerSession PlayingSession(SessionManager sessions, FakeConnection conn,
        string name = "TestPlayer")
    {
        var session = new PlayerSession(conn, new Entity("player", name));
        session.Phase = LoginPhase.Playing;
        sessions.Add(session);
        return session;
    }

    [Fact]
    public void Heartbeat_sweep_pings_each_playing_session()
    {
        var sessions = new SessionManager();
        var eventQueue = new SystemEventQueue();
        var gameLoop = NewGameLoop(sessions, eventQueue);

        var conn = new FakeConnection();
        PlayingSession(sessions, conn);

        gameLoop.RegisterHeartbeatHandler(sessions, intervalTicks: 1);
        gameLoop.Tick();

        conn.HeartbeatCount.Should().Be(1);
    }

    [Fact]
    public void Heartbeat_sweep_skips_non_playing_sessions()
    {
        var sessions = new SessionManager();
        var eventQueue = new SystemEventQueue();
        var gameLoop = NewGameLoop(sessions, eventQueue);

        var conn = new FakeConnection();
        var session = new PlayerSession(conn, new Entity("player", "Creating"));
        session.Phase = LoginPhase.Creating;
        sessions.Add(session);

        gameLoop.RegisterHeartbeatHandler(sessions, intervalTicks: 1);
        gameLoop.Tick();

        conn.HeartbeatCount.Should().Be(0);
    }

    [Fact]
    public void Heartbeat_to_dead_peer_disconnects_the_connection()
    {
        var sessions = new SessionManager();
        var eventQueue = new SystemEventQueue();
        var gameLoop = NewGameLoop(sessions, eventQueue);

        // Simulate a half-open peer: the heartbeat write fails, which (like the real
        // TelnetConnection) routes to Disconnect and fires the disconnect events.
        var conn = new FakeConnection { FailHeartbeat = true };
        PlayingSession(sessions, conn);

        var disconnectReason = (string?)null;
        conn.OnDisconnectedWithReason += r => disconnectReason = r;

        gameLoop.RegisterHeartbeatHandler(sessions, intervalTicks: 1);
        gameLoop.Tick();

        conn.IsConnected.Should().BeFalse();
        disconnectReason.Should().Be("heartbeat write error");
    }

    [Fact]
    public void Afk_idle_kick_force_disconnects_a_stale_playing_session()
    {
        var sessions = new SessionManager();
        var eventQueue = new SystemEventQueue();
        var gameLoop = NewGameLoop(sessions, eventQueue, ticksPerSecond: 10);

        var conn = new FakeConnection();
        var session = PlayingSession(sessions, conn);
        // Last input long ago: stale relative to the 1s (=10 tick) timeout below.
        session.UpdateLastInputTick(0);

        gameLoop.ConfigureIdleTimeout(warnSeconds: 0, timeoutSeconds: 1);
        gameLoop.RegisterIdleTimeoutHandler(eventQueue, sessions, "warn", "You faded.");

        // Advance well past the timeout window, then run the idle sweep (300-tick interval).
        for (var i = 0; i < 300; i++)
        {
            gameLoop.Tick();
        }

        conn.IsConnected.Should().BeFalse("an AFK playing session past the idle timeout is force-disconnected");
    }

    [Fact]
    public void Afk_idle_kick_leaves_a_fresh_playing_session_connected()
    {
        var sessions = new SessionManager();
        var eventQueue = new SystemEventQueue();
        var gameLoop = NewGameLoop(sessions, eventQueue, ticksPerSecond: 10);

        var conn = new FakeConnection();
        var session = PlayingSession(sessions, conn);

        gameLoop.ConfigureIdleTimeout(warnSeconds: 0, timeoutSeconds: 1000);
        gameLoop.RegisterIdleTimeoutHandler(eventQueue, sessions, "warn", "You faded.");

        // Keep input recent every tick so idleTicks never reaches the timeout.
        for (var i = 0; i < 300; i++)
        {
            session.UpdateLastInputTick(gameLoop.TickCount);
            gameLoop.Tick();
        }

        conn.IsConnected.Should().BeTrue("a session with recent input must not be AFK-kicked");
    }

    [Fact]
    public void Afk_idle_kick_does_not_touch_non_playing_sessions()
    {
        var sessions = new SessionManager();
        var eventQueue = new SystemEventQueue();
        var gameLoop = NewGameLoop(sessions, eventQueue, ticksPerSecond: 10);

        var conn = new FakeConnection();
        var session = new PlayerSession(conn, new Entity("player", "Chargen"));
        session.Phase = LoginPhase.Creating;
        session.UpdateLastInputTick(0);
        sessions.Add(session);

        gameLoop.ConfigureIdleTimeout(warnSeconds: 0, timeoutSeconds: 1);
        gameLoop.RegisterIdleTimeoutHandler(eventQueue, sessions, "warn", "You faded.");

        for (var i = 0; i < 300; i++)
        {
            gameLoop.Tick();
        }

        conn.IsConnected.Should().BeTrue("the AFK kick must not disconnect a session still in chargen");
    }
}
