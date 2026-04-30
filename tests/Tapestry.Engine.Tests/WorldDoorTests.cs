using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests;

public class WorldDoorTests
{
    [Fact]
    public void MoveEntity_ClosedDoor_ReturnsFalseAndPublishesBlocked()
    {
        var world = new World();
        var eventBus = new EventBus();
        var doorService = new DoorService(world, eventBus);

        var roomA = new Room("test:a", "A", "");
        var roomB = new Room("test:b", "B", "");
        roomA.SetExit(Direction.North, new Exit("test:b")
        {
            Door = new DoorState { Name = "door", IsClosed = true }
        });
        world.AddRoom(roomA);
        world.AddRoom(roomB);

        var player = new Entity("player", "Rand");
        roomA.AddEntity(player);

        GameEvent? blocked = null;
        eventBus.Subscribe("door.blocked", e => { blocked = e; });

        var result = world.MoveEntity(player, Direction.North, doorService, eventBus);

        result.Should().BeFalse();
        player.LocationRoomId.Should().Be("test:a");
        blocked.Should().NotBeNull();
    }

    [Fact]
    public void MoveEntity_OpenDoor_AllowsMovement()
    {
        var world = new World();
        var eventBus = new EventBus();
        var doorService = new DoorService(world, eventBus);

        var roomA = new Room("test:a", "A", "");
        var roomB = new Room("test:b", "B", "");
        roomA.SetExit(Direction.North, new Exit("test:b")
        {
            Door = new DoorState { Name = "door", IsClosed = false }
        });
        world.AddRoom(roomA);
        world.AddRoom(roomB);

        var player = new Entity("player", "Rand");
        roomA.AddEntity(player);

        var result = world.MoveEntity(player, Direction.North, doorService, eventBus);

        result.Should().BeTrue();
        player.LocationRoomId.Should().Be("test:b");
    }

    [Fact]
    public void MoveEntity_NoDoor_AllowsMovement()
    {
        var world = new World();
        var eventBus = new EventBus();
        var doorService = new DoorService(world, eventBus);

        var roomA = new Room("test:a", "A", "");
        var roomB = new Room("test:b", "B", "");
        roomA.SetExit(Direction.North, new Exit("test:b"));
        world.AddRoom(roomA);
        world.AddRoom(roomB);

        var player = new Entity("player", "Rand");
        roomA.AddEntity(player);

        var result = world.MoveEntity(player, Direction.North, doorService, eventBus);

        result.Should().BeTrue();
    }
}
