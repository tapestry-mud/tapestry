using FluentAssertions;
using Tapestry.Engine.Login;
using Tapestry.Engine.Watch;

namespace Tapestry.Engine.Tests.Watch;

public class WatchRosterSourceTests
{
    [Fact]
    public void Snapshot_IncludesPlayingPlayer_WithIdNameRoom()
    {
        var sessions = new SessionManager();
        var entity = new Entity("player", "Mallek") { LocationRoomId = "lf:square" };
        sessions.Add(new PlayerSession(new FakeConnection(), entity) { Phase = LoginPhase.Playing });

        var snap = new WatchRosterSource(sessions).Snapshot();

        snap.Should().ContainSingle();
        snap[0].EntityId.Should().Be(entity.Id.ToString());
        snap[0].Name.Should().Be("Mallek");
        snap[0].RoomId.Should().Be("lf:square");
    }

    [Fact]
    public void Snapshot_ExcludesNonPlayingPhases()
    {
        var sessions = new SessionManager();
        sessions.Add(new PlayerSession(new FakeConnection(), new Entity("player", "Creating"))
        { Phase = LoginPhase.Creating });
        sessions.Add(new PlayerSession(new FakeConnection(), new Entity("player", "LinkDead"))
        { Phase = LoginPhase.LinkDead });

        new WatchRosterSource(sessions).Snapshot().Should().BeEmpty();
    }

    [Fact]
    public void Snapshot_NullRoom_BecomesEmptyString()
    {
        var sessions = new SessionManager();
        sessions.Add(new PlayerSession(new FakeConnection(), new Entity("player", "Roomless"))
        { Phase = LoginPhase.Playing });

        new WatchRosterSource(sessions).Snapshot()[0].RoomId.Should().BeEmpty();
    }
}
