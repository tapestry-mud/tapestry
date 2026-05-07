using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Flow;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class WorldTests
{
    [Fact]
    public void AddRoom_AndGetById()
    {
        var world = new World();
        var room = new Room("town:square", "Town Square", "A square.");
        world.AddRoom(room);
        world.GetRoom("town:square").Should().Be(room);
    }

    [Fact]
    public void GetRoom_ReturnsNull_WhenNotFound()
    {
        var world = new World();
        world.GetRoom("nonexistent").Should().BeNull();
    }

    [Fact]
    public void RemoveRoom_RemovesIt()
    {
        var world = new World();
        var room = new Room("town:square", "Town Square", "A square.");
        world.AddRoom(room);
        world.RemoveRoom("town:square");
        world.GetRoom("town:square").Should().BeNull();
    }

    [Fact]
    public void MoveEntity_BetweenRooms()
    {
        var world = new World();
        var square = new Room("town:square", "Town Square", "A square.");
        var inn = new Room("town:inn", "The Inn", "A cozy inn.");
        square.SetExit(Direction.North, new Exit("town:inn"));
        inn.SetExit(Direction.South, new Exit("town:square"));
        world.AddRoom(square);
        world.AddRoom(inn);
        var player = new Entity("player", "Rand");
        square.AddEntity(player);
        var moved = world.MoveEntity(player, Direction.North);
        moved.Should().BeTrue();
        inn.Entities.Should().Contain(player);
        square.Entities.Should().NotContain(player);
        player.LocationRoomId.Should().Be("town:inn");
    }

    [Fact]
    public void MoveEntity_ReturnsFalse_WhenNoExit()
    {
        var world = new World();
        var square = new Room("town:square", "Town Square", "A square.");
        world.AddRoom(square);
        var player = new Entity("player", "Rand");
        square.AddEntity(player);
        var moved = world.MoveEntity(player, Direction.North);
        moved.Should().BeFalse();
        square.Entities.Should().Contain(player);
    }

    [Fact]
    public void GetEntitiesByTag_ReturnsMatching()
    {
        var world = new World();
        var room = new Room("test", "Test", "Test.");
        world.AddRoom(room);
        var hostile = new Entity("mob", "Elf");
        hostile.AddTag("hostile");
        room.AddEntity(hostile);
        world.TrackEntity(hostile);
        var friendly = new Entity("npc", "Vendor");
        friendly.AddTag("friendly");
        room.AddEntity(friendly);
        world.TrackEntity(friendly);
        world.SwapTagBuffers();

        world.GetEntitiesByTag("hostile").Should().Contain(hostile);
        world.GetEntitiesByTag("hostile").Should().NotContain(friendly);
    }

    [Fact]
    public void GetEntity_FindsByIdAcrossRooms()
    {
        var world = new World();
        var room = new Room("test", "Test", "Test.");
        world.AddRoom(room);
        var entity = new Entity("player", "Rand");
        room.AddEntity(entity);
        world.GetEntity(entity.Id).Should().Be(entity);
        world.GetEntity(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void GetEntity_FallsThrough_ToPlayerCreator()
    {
        var creator = new PlayerCreator();
        var world = new World(creator);
        var entity = new Entity("player", "PendingPlayer");
        creator.TrackEntity(entity);
        Assert.Equal(entity, world.GetEntity(entity.Id));
    }
}
