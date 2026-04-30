using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Scripting.Services;

namespace Tapestry.Scripting.Tests;

public class ApiMessagingTests
{
    [Fact]
    public void SendToRoomExceptMany_ExcludesSpecifiedEntities()
    {
        var world = new World();
        var sessions = new SessionManager();
        var messaging = new ApiMessaging(world, sessions);

        // Set up a room with two players
        var room = new Room("core:arena", "Arena", "The arena.");
        world.AddRoom(room);

        var conn1 = new FakeConnection();
        var entity1 = new Entity("player", "Player1");
        room.AddEntity(entity1);
        sessions.Add(new PlayerSession(conn1, entity1));

        var conn2 = new FakeConnection();
        var entity2 = new Entity("player", "Player2");
        room.AddEntity(entity2);
        sessions.Add(new PlayerSession(conn2, entity2));

        var conn3 = new FakeConnection();
        var entity3 = new Entity("player", "Player3");
        room.AddEntity(entity3);
        sessions.Add(new PlayerSession(conn3, entity3));

        var excludeIds = new[] { entity1.Id.ToString(), entity2.Id.ToString() };
        messaging.SendToRoomExceptMany("core:arena", excludeIds, "Combat message");

        // Only entity3 should receive the message
        conn1.SentText.Should().BeEmpty();
        conn2.SentText.Should().BeEmpty();
        conn3.SentText.Should().NotBeEmpty();
        string.Join("", conn3.SentText).Should().Contain("Combat message");
    }

    [Fact]
    public void SendToRoomExceptMany_SkipsInvalidGuids()
    {
        var world = new World();
        var sessions = new SessionManager();
        var messaging = new ApiMessaging(world, sessions);

        var room = new Room("core:arena", "Arena", "The arena.");
        world.AddRoom(room);

        var conn1 = new FakeConnection();
        var entity1 = new Entity("player", "Player1");
        room.AddEntity(entity1);
        sessions.Add(new PlayerSession(conn1, entity1));

        // Pass one valid ID + invalid strings -- only entity1 should be excluded
        var idStrings = new[] { entity1.Id.ToString(), "not-a-guid", "" };
        messaging.SendToRoomExceptMany("core:arena", idStrings, "Test");

        // entity1 is excluded, and it's the only player, so nobody gets the message
        conn1.SentText.Should().BeEmpty();
    }

    [Fact]
    public void SendToAll_SetsNeedsPromptRefresh_OnReceivingSession()
    {
        var world = new World();
        var sessions = new SessionManager();
        var messaging = new ApiMessaging(world, sessions);

        var conn1 = new FakeConnection();
        var entity1 = new Entity("player", "Player1");
        var session1 = new PlayerSession(conn1, entity1);
        sessions.Add(session1);

        var conn2 = new FakeConnection();
        var entity2 = new Entity("player", "Player2");
        var session2 = new PlayerSession(conn2, entity2);
        sessions.Add(session2);

        // Exclude session1; session2 should receive and have NeedsPromptRefresh set
        messaging.SendToAll("Gossip message", entity1.Id.ToString());

        session1.NeedsPromptRefresh.Should().BeFalse();
        session2.NeedsPromptRefresh.Should().BeTrue();
        string.Join("", conn2.SentText).Should().Contain("Gossip message");
        conn1.SentText.Should().BeEmpty();
    }
}
