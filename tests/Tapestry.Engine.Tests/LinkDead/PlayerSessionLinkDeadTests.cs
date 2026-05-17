using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Tests;

namespace Tapestry.Engine.Tests.LinkDead;

public class PlayerSessionLinkDeadTests
{
    private static Entity MakeEntity(string name = "Tester") =>
        new Entity("player", name);

    [Fact]
    public void ReplaceConnection_UpdatesConnectionReference()
    {
        var old = new FakeConnection("old-id");
        var session = new PlayerSession(old, MakeEntity());

        var newConn = new FakeConnection("new-id");
        session.ReplaceConnection(newConn);

        session.Connection.Id.Should().Be("new-id");
    }

    [Fact]
    public void ReplaceConnection_NewConnectionReceivesInput()
    {
        var old = new FakeConnection("old-id");
        var session = new PlayerSession(old, MakeEntity());
        session.Phase = LoginPhase.Playing;

        var newConn = new FakeConnection("new-id");
        session.ReplaceConnection(newConn);

        newConn.SimulateInput("look");

        session.TryDequeueInput(out var cmd).Should().BeTrue();
        cmd.Should().Be("look");
    }

    [Fact]
    public void LinkDeadSinceTick_DefaultsToZero()
    {
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, MakeEntity());

        session.LinkDeadSinceTick.Should().Be(0);
    }

    [Fact]
    public void LinkDeadSinceTick_CanBeSet()
    {
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, MakeEntity());

        session.LinkDeadSinceTick = 5000L;

        session.LinkDeadSinceTick.Should().Be(5000L);
    }

    [Fact]
    public void ClearInputQueue_RemovesQueuedCommands()
    {
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, MakeEntity());
        session.Phase = LoginPhase.Playing;
        conn.SimulateInput("north");
        conn.SimulateInput("look");

        session.ClearInputQueue();

        session.TryDequeueInput(out _).Should().BeFalse();
    }
}
