using System.Linq;
using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Login;
using Tapestry.Server.Tests.Fakes;
using Tapestry.Shared;

namespace Tapestry.Server.Tests;

public class NewCharacterTeardownBridgeTests
{
    [Fact]
    public void NewCharacter_FirstQuit_EnqueuesDisconnectEvent_SoSessionIsTornDown()
    {
        var sessions = new SessionManager();
        var queue = new SystemEventQueue();
        var bus = new EventBus();
        _ = new NewCharacterTeardownBridge(bus, sessions, queue);

        var conn = new FakeConnection();
        var entity = new Entity("player", "Newbie");
        var session = new PlayerSession(conn, entity) { Phase = LoginPhase.Creating };
        sessions.Add(session);

        // Chargen completes: FlowEngine publishes character.created, then flips to Playing.
        bus.Publish(new GameEvent { Type = "character.created", SourceEntityId = entity.Id });
        session.Phase = LoginPhase.Playing;

        // The brand-new player's very first quit.
        conn.Disconnect("Quit");

        var disconnects = queue.DrainAll().OfType<DisconnectEvent>().ToList();
        disconnects.Should().ContainSingle(e => e.EntityId == entity.Id,
            "a newly created character's first quit must enqueue the teardown that removes the session");
    }

    [Fact]
    public void DisconnectBeforeCharacterCreated_DoesNotEnqueue()
    {
        // Mid-creation abandon is handled by the Creating-gated handler at the creation
        // site, never by this bridge. The bridge must stay inert until character.created.
        var sessions = new SessionManager();
        var queue = new SystemEventQueue();
        var bus = new EventBus();
        _ = new NewCharacterTeardownBridge(bus, sessions, queue);

        var conn = new FakeConnection();
        var entity = new Entity("player", "Quitter");
        var session = new PlayerSession(conn, entity) { Phase = LoginPhase.Creating };
        sessions.Add(session);

        conn.Disconnect("connection closed"); // abandoned before completing creation

        queue.DrainAll().OfType<DisconnectEvent>().Should().BeEmpty();
    }
}
