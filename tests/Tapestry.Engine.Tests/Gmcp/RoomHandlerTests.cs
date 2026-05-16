using FluentAssertions;
using Tapestry.Shared;
using Tapestry.Engine;
using Tapestry.Engine.Quests;
using Tapestry.Server.Gmcp;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Engine.Tests.Gmcp;

public class RoomHandlerTests
{
    private record Harness(
        RoomHandler Handler,
        FakeGmcpConnectionManager ConnectionManager,
        SessionManager Sessions,
        EventBus EventBus,
        Entity Player,
        Room SpawnRoom,
        string ConnectionId);

    private static Harness Build(bool addExitRoom = false)
    {
        var cm = new FakeGmcpConnectionManager();
        var sessions = new SessionManager();
        var world = new World();
        var eb = new EventBus();

        var repo = new QuestStateRepository();
        var registry = new QuestRegistry();
        var questMarkers = new QuestMarkerService(repo, registry);
        var handler = new RoomHandler(cm, sessions, world, eb, questMarkers);

        var room = new Room("test:room1", "Test Room", "A plain room.");
        world.AddRoom(room);

        if (addExitRoom)
        {
            var northRoom = new Room("test:room2", "North Room", "A room to the north.");
            world.AddRoom(northRoom);
            room.SetExit(Direction.North, new Exit("test:room2"));
        }

        var entity = new Entity("player", "Hero");
        entity.LocationRoomId = room.Id;
        world.TrackEntity(entity);
        room.AddEntity(entity);

        var conn = new FakeConnection();
        sessions.Add(new PlayerSession(conn, entity));
        handler.Configure();

        return new Harness(handler, cm, sessions, eb, entity, room, conn.Id);
    }

    [Fact]
    public void SendBurst_SendsRoomNearbyBeforeRoomInfo()
    {
        var h = Build();

        h.Handler.SendBurst(h.ConnectionId, h.Player);

        var nearbyIndex = h.ConnectionManager.Sent.FindIndex(x => x.Package == "Room.Nearby");
        var infoIndex = h.ConnectionManager.Sent.FindIndex(x => x.Package == "Room.Info");
        nearbyIndex.Should().BeLessThan(infoIndex);
    }

    [Fact]
    public void PlayerMovedEvent_SendsRoomInfoAndNearby()
    {
        var h = Build();

        h.EventBus.Publish(new GameEvent
        {
            Type = "player.moved",
            SourceEntityId = h.Player.Id
        });

        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Room.Info");
        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Room.Nearby");
    }

    [Fact]
    public void PlayerMoveFailedEvent_SendsRoomWrongDir()
    {
        var h = Build();

        h.EventBus.Publish(new GameEvent
        {
            Type = "player.move.failed",
            SourceEntityId = h.Player.Id
        });

        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Room.WrongDir");
    }

    [Fact]
    public void PackageNames_ContainsAllThreePackages()
    {
        var h = Build();
        h.Handler.PackageNames.Should().Contain("Room.Info")
            .And.Contain("Room.Nearby")
            .And.Contain("Room.WrongDir");
    }

    [Fact]
    public void RoomInfo_ExitsContainIdAndName()
    {
        var h = Build(addExitRoom: true);

        h.Handler.SendBurst(h.ConnectionId, h.Player);

        var infoMsg = h.ConnectionManager.Sent.First(x => x.Package == "Room.Info");
        var json = System.Text.Json.JsonSerializer.Serialize(infoMsg.Payload);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var exits = doc.RootElement.GetProperty("exits");
        var north = exits.GetProperty("n");
        north.GetProperty("id").GetString().Should().Be("test:room2");
        north.GetProperty("name").GetString().Should().Be("North Room");
    }
}
