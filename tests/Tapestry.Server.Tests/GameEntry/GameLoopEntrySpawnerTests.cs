using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Login;
using Tapestry.Engine.Persistence;
using Tapestry.Server.GameEntry;
using Tapestry.Server.Tests.Fakes;
using Tapestry.Shared;

namespace Tapestry.Server.Tests.GameEntry;

/// <summary>
/// Login runs on a thread-pool thread (ConnectionHandler's Task.Run), but committing a
/// character into the world publishes engine events that dispatch into the single shared,
/// unsynchronized Jint engine. Doing that concurrently with the game loop's own script
/// invocation corrupts Jint mid-call: observed on prod as a NullReferenceException raised
/// inside Jint.Native.Function.ScriptFunction.Call under a character.created dispatch, which
/// silently destroyed the whole content-side login hook (no recall point set, no opener) for
/// roughly one in ten fresh characters. This decorator is the barrier: every game-entry
/// commit is posted to the loop and runs inside Tick, never on the login thread.
/// </summary>
public class GameLoopEntrySpawnerTests
{
    private static GameLoop BuildLoop(SessionManager sessions)
    {
        return new GameLoop(
            router: null!, sessions, new EventBus(), new SystemEventQueue(),
            NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10),
            new NotificationQueue(), registrationPolicy: null!);
    }

    private static (GameLoopEntrySpawner spawner, FakeGameEntrySpawner inner, GameLoop loop) Build()
    {
        var sessions = new SessionManager();
        var loop = BuildLoop(sessions);
        var inner = new FakeGameEntrySpawner();
        return (new GameLoopEntrySpawner(inner, loop), inner, loop);
    }

    [Fact]
    public void CompleteLogin_does_not_run_on_the_calling_thread()
    {
        var (spawner, inner, loop) = Build();

        spawner.CompleteLogin(new Entity("player", "Alice"), new FakeConnection(),
            new LoginContext(Guid.NewGuid().ToString(), new FakeConnection()), Guid.NewGuid());

        inner.CompleteLoginCalled.Should().BeFalse(
            "the login thread must not commit a character into the world itself");

        loop.Tick();

        inner.CompleteLoginCalled.Should().BeTrue(
            "the commit must land on the game loop, where script dispatch is serialized");
    }

    [Fact]
    public void ReconnectLinkDead_does_not_run_on_the_calling_thread()
    {
        var (spawner, inner, loop) = Build();
        var session = new PlayerSession(new FakeConnection(), new Entity("player", "Bob"));

        spawner.ReconnectLinkDead(session, new FakeConnection(),
            new LoginContext(Guid.NewGuid().ToString(), new FakeConnection()));

        inner.ReconnectCalled.Should().BeFalse();
        loop.Tick();
        inner.ReconnectCalled.Should().BeTrue();
    }

    [Fact]
    public void TakeOverSession_does_not_run_on_the_calling_thread()
    {
        var (spawner, inner, loop) = Build();
        var session = new PlayerSession(new FakeConnection(), new Entity("player", "Cara"));

        spawner.TakeOverSession(session, new FakeConnection(),
            new LoginContext(Guid.NewGuid().ToString(), new FakeConnection()));

        inner.TakeOverCalled.Should().BeFalse();
        loop.Tick();
        inner.TakeOverCalled.Should().BeTrue();
    }

    [Fact]
    public void RestoreWorldObjects_runs_before_the_CompleteLogin_it_precedes()
    {
        var sessions = new SessionManager();
        var loop = BuildLoop(sessions);
        var order = new List<string>();
        var inner = new RecordingSpawner(order);
        var spawner = new GameLoopEntrySpawner(inner, loop);

        // GameEntryResolver's fresh-spawn path calls these back to back; the restored items
        // must be tracked before the entity that owns them is committed into a room.
        spawner.RestoreWorldObjects(new PlayerLoadResult
        {
            Entity = new Entity("player", "Dana"),
            AccountId = Guid.NewGuid(),
            AllItems = new List<Entity>()
        });
        spawner.CompleteLogin(new Entity("player", "Dana"), new FakeConnection(),
            new LoginContext(Guid.NewGuid().ToString(), new FakeConnection()), Guid.NewGuid());

        order.Should().BeEmpty();

        loop.Tick();

        order.Should().Equal("restore", "login");
    }

    private class RecordingSpawner : IGameEntrySpawner
    {
        private readonly List<string> _order;

        public RecordingSpawner(List<string> order)
        {
            _order = order;
        }

        public void RestoreWorldObjects(PlayerLoadResult data)
        {
            _order.Add("restore");
        }

        public void CompleteLogin(Entity entity, IConnection connection, LoginContext preLogin, Guid accountId)
        {
            _order.Add("login");
        }

        public void TakeOverSession(PlayerSession existing, IConnection newConnection, LoginContext preLogin)
        {
            _order.Add("takeover");
        }

        public void ReconnectLinkDead(PlayerSession session, IConnection newConnection, LoginContext preLogin)
        {
            _order.Add("reconnect");
        }
    }
}
