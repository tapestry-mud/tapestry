using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class SessionManagerTests
{
    private SessionManager _sessions = null!;

    private void Setup()
    {
        _sessions = new SessionManager();
    }

    private (PlayerSession session, FakeConnection connection) AddPlayer(string name, string roomId)
    {
        var connection = new FakeConnection();
        var entity = new Entity("player", name);
        entity.AddTag("player");
        var room = new Room(roomId, roomId, "A room.");
        room.AddEntity(entity);
        var session = new PlayerSession(connection, entity);
        _sessions.Add(session);
        return (session, connection);
    }

    [Fact]
    public void SendToRoom_ExcludesMultipleEntities()
    {
        Setup();
        var (alice, aliceConn) = AddPlayer("Alice", "room1");
        var (bob, bobConn) = AddPlayer("Bob", "room1");
        var (carol, carolConn) = AddPlayer("Carol", "room1");

        var excludes = new HashSet<Guid> { alice.PlayerEntity.Id, bob.PlayerEntity.Id };
        _sessions.SendToRoom("room1", "Hello room.", excludes);

        Assert.Empty(aliceConn.SentText);
        Assert.Empty(bobConn.SentText);
        Assert.Single(carolConn.SentText);
        Assert.Equal("Hello room.", carolConn.SentText[0]);
    }

    [Fact]
    public void SendToRoom_EmptyExcludes_SendsToAll()
    {
        Setup();
        var (alice, aliceConn) = AddPlayer("Alice", "room1");
        var (bob, bobConn) = AddPlayer("Bob", "room1");

        var excludes = new HashSet<Guid>();
        _sessions.SendToRoom("room1", "Broadcast.", excludes);

        Assert.Single(aliceConn.SentText);
        Assert.Equal("Broadcast.", aliceConn.SentText[0]);
        Assert.Single(bobConn.SentText);
        Assert.Equal("Broadcast.", bobConn.SentText[0]);
    }

    [Fact]
    public void SendToRoom_SingleExclude_BackwardsCompatible()
    {
        Setup();
        var (alice, aliceConn) = AddPlayer("Alice", "room1");
        var (bob, bobConn) = AddPlayer("Bob", "room1");

        _sessions.SendToRoom("room1", "Single exclude.", alice.PlayerEntity.Id);

        Assert.Empty(aliceConn.SentText);
        Assert.Single(bobConn.SentText);
        Assert.Equal("Single exclude.", bobConn.SentText[0]);
    }

    [Fact]
    public void GetByPlayerName_ReturnsSession()
    {
        Setup();
        var (session, _) = AddPlayer("Krakus", "room1");

        _sessions.GetByPlayerName("Krakus").Should().Be(session);
        _sessions.GetByPlayerName("krakus").Should().Be(session);
    }

    [Fact]
    public void GetByPlayerName_ReturnsNull_AfterRemove()
    {
        Setup();
        var (session, _) = AddPlayer("Krakus", "room1");
        _sessions.Remove(session);

        _sessions.GetByPlayerName("Krakus").Should().BeNull();
    }
}
