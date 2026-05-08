using FluentAssertions;
using Tapestry.Data;
using Tapestry.Engine;
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

    private static Harness Build()
    {
        var cm = new FakeGmcpConnectionManager();
        var sessions = new SessionManager();
        var world = new World();
        var eb = new EventBus();

        var handler = new RoomHandler(cm, sessions, world, eb);

        var room = new Room("test:room1", "Test Room", "A plain room.");
        world.AddRoom(room);

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
}
