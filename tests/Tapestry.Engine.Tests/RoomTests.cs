using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class RoomTests
{
    [Fact]
    public void Room_HasIdNameDescription()
    {
        var room = new Room("town:square", "Town Square", "A bustling town square.");
        room.Id.Should().Be("town:square");
        room.Name.Should().Be("Town Square");
        room.Description.Should().Be("A bustling town square.");
    }

    [Fact]
    public void Room_SetAndGetExit()
    {
        var room = new Room("town:square", "Town Square", "A square.");
        room.SetExit(Direction.North, new Exit("town:inn"));
        room.GetExit(Direction.North).Should().NotBeNull();
        room.GetExit(Direction.North)!.TargetRoomId.Should().Be("town:inn");
        room.GetExit(Direction.South).Should().BeNull();
    }

    [Fact]
    public void Room_AddAndRemoveEntity()
    {
        var room = new Room("town:square", "Town Square", "A square.");
        var player = new Entity("player", "Rand");
        room.AddEntity(player);
        room.Entities.Should().Contain(player);
        player.LocationRoomId.Should().Be("town:square");
        room.RemoveEntity(player);
        room.Entities.Should().NotContain(player);
        player.LocationRoomId.Should().BeNull();
    }

    [Fact]
    public void Room_AddEntity_DoesNotDuplicate_WhenCalledTwice()
    {
        var room = new Room("town:square", "Town Square", "A square.");
        var player = new Entity("player", "Rand");
        room.AddEntity(player);
        room.AddEntity(player);
        room.Entities.Should().HaveCount(1);
    }

    [Fact]
    public void Room_AddEntity_DoesNotLeakStaleEntry_AfterMoveAndReAdd()
    {
        var roomA = new Room("town:inn", "Inn", ".");
        var roomB = new Room("town:stable", "Stable", ".");
        var player = new Entity("player", "Rand");

        roomA.AddEntity(player);
        roomA.AddEntity(player);

        roomA.RemoveEntity(player);
        roomB.AddEntity(player);

        roomA.Entities.Should().NotContain(player);
        roomB.Entities.Should().Contain(player);
        player.LocationRoomId.Should().Be("town:stable");
    }

    [Fact]
    public void Room_Tags()
    {
        var room = new Room("town:square", "Town Square", "A square.");
        room.AddTag("safe");
        room.AddTag("recall-point");
        room.HasTag("safe").Should().BeTrue();
        room.HasTag("dangerous").Should().BeFalse();
    }

    [Fact]
    public void Room_AvailableExits()
    {
        var room = new Room("town:square", "Town Square", "A square.");
        room.SetExit(Direction.North, new Exit("town:inn"));
        room.SetExit(Direction.East, new Exit("town:market"));
        var exits = room.AvailableExits().ToList();
        exits.Should().HaveCount(2);
        exits.Should().Contain(Direction.North);
        exits.Should().Contain(Direction.East);
    }
}
