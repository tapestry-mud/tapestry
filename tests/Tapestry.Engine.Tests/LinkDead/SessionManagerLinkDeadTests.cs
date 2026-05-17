using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Prompt;
using Tapestry.Engine.Tests;

namespace Tapestry.Engine.Tests.LinkDead;

public class SessionManagerLinkDeadTests
{
    private static PlayerSession MakeSession(string connId, string name = "Tester",
        LoginPhase phase = LoginPhase.Playing)
    {
        var conn = new FakeConnection(connId);
        var entity = new Entity("player", name);
        var session = new PlayerSession(conn, entity);
        session.Phase = phase;
        return session;
    }

    [Fact]
    public void AllLinkDeadSessions_ReturnsOnlyLinkDeadPhase()
    {
        var sm = new SessionManager();
        var playing = MakeSession("c1", "Alpha");
        var linkdead = MakeSession("c2", "Beta", LoginPhase.LinkDead);

        sm.Add(playing);
        sm.Add(linkdead);
        sm.RemoveConnectionOnly(linkdead);

        sm.AllLinkDeadSessions.Should().ContainSingle(s => s.PlayerEntity.Name == "Beta");
        sm.AllLinkDeadSessions.Should().NotContain(s => s.PlayerEntity.Name == "Alpha");
    }

    [Fact]
    public void ReRegisterConnectionForSession_AddsNewConnectionToByConnectionId()
    {
        var sm = new SessionManager();
        var session = MakeSession("old-conn");
        sm.Add(session);
        sm.RemoveConnectionOnly(session);

        var newConn = new FakeConnection("new-conn");
        session.ReplaceConnection(newConn);
        sm.ReRegisterConnectionForSession(session);

        sm.GetByConnectionId("new-conn").Should().BeSameAs(session);
        sm.GetByConnectionId("old-conn").Should().BeNull();
    }

    [Fact]
    public void FlushPrompts_SkipsLinkDeadSessions()
    {
        var sm = new SessionManager();
        var conn = new FakeConnection("c1");
        var entity = new Entity("player", "Alpha");
        var session = new PlayerSession(conn, entity);
        session.Phase = LoginPhase.LinkDead;
        session.NeedsPromptRefresh = true;
        sm.Add(session);
        sm.RemoveConnectionOnly(session);

        sm.FlushPrompts(new PromptRenderer());

        session.NeedsPromptRefresh.Should().BeTrue();
    }

    [Fact]
    public void RemoveConnectionOnly_KeepsEntityAndNameLookups()
    {
        var sm = new SessionManager();
        var session = MakeSession("c1", "Bravo");
        sm.Add(session);

        sm.RemoveConnectionOnly(session);

        sm.GetByConnectionId("c1").Should().BeNull();
        sm.GetByEntityId(session.PlayerEntity.Id).Should().BeSameAs(session);
        sm.GetByPlayerName("Bravo").Should().BeSameAs(session);
    }
}
