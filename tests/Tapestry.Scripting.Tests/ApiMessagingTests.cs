using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Quests;
using Tapestry.Scripting.Modules;
using Tapestry.Scripting.Services;
using Tapestry.Shared;

namespace Tapestry.Scripting.Tests;

public class ApiMessagingTests
{
    private static ApiMessaging CreateMessaging(World world, SessionManager sessions)
    {
        var questMarkerService = new QuestMarkerService(new QuestStateRepository(), new QuestRegistry());
        return new ApiMessaging(world, sessions, new NullGmcpModuleAdapter(), new CommandResponseContext(), new VisibilityFilter(), questMarkerService);
    }

    [Fact]
    public void SendToRoomExceptMany_ExcludesSpecifiedEntities()
    {
        var world = new World();
        var sessions = new SessionManager();
        var messaging = CreateMessaging(world, sessions);

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
        var messaging = CreateMessaging(world, sessions);

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

    // --- SendRoomDescription brief flag (brief mode v1, tapestry#42 engine half) ---

    private static (ApiMessaging Messaging, FakeConnection Conn, Entity Player) BriefFixture()
    {
        var world = new World();
        var sessions = new SessionManager();
        var messaging = CreateMessaging(world, sessions);

        var room = new Room("core:larder", "Quiet Larder", "Flour dusts every surface like fresh snow.");
        room.SetExit(Direction.North, new Exit("core:cellar"));
        world.AddRoom(room);

        var conn = new FakeConnection();
        var player = new Entity("player", "Rocky");
        room.AddEntity(player);
        world.TrackEntity(player);
        sessions.Add(new PlayerSession(conn, player));

        var npc = new Entity("npc", "angry cook");
        room.AddEntity(npc);
        world.TrackEntity(npc);

        return (messaging, conn, player);
    }

    [Fact]
    public void SendRoomDescription_Brief_SuppressesBodyKeepsNameExitsEntities()
    {
        var (messaging, conn, player) = BriefFixture();

        messaging.SendRoomDescription(player.Id.ToString(), brief: true);

        var text = string.Join("", conn.SentText);
        text.Should().Contain("Quiet Larder");          // room name stays
        text.Should().NotContain("Flour dusts");        // description body suppressed
        text.Should().Contain("[Exits:");               // exits line stays
        text.Should().Contain("angry cook is here.");   // entity lines stay
    }

    [Fact]
    public void SendRoomDescription_DefaultCall_IsFullRender()
    {
        var (messaging, conn, player) = BriefFixture();

        messaging.SendRoomDescription(player.Id.ToString());

        var text = string.Join("", conn.SentText);
        text.Should().Contain("Quiet Larder");
        text.Should().Contain("Flour dusts every surface like fresh snow.");
        text.Should().Contain("[Exits:");
        text.Should().Contain("angry cook is here.");
    }

    [Fact]
    public void SendRoomDescription_BriefFalse_IsByteIdenticalToDefaultCall()
    {
        var (messagingA, connA, playerA) = BriefFixture();
        messagingA.SendRoomDescription(playerA.Id.ToString());

        var (messagingB, connB, playerB) = BriefFixture();
        messagingB.SendRoomDescription(playerB.Id.ToString(), brief: false);

        string.Join("", connB.SentText).Should().Be(string.Join("", connA.SentText));
    }

    [Fact]
    public void SendToAll_SetsNeedsPromptRefresh_OnReceivingSession()
    {
        var world = new World();
        var sessions = new SessionManager();
        var messaging = CreateMessaging(world, sessions);

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
