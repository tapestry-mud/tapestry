using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class WorldRekeyRoomTests
{
    private static Room AddRoom(World world, string id, string name, string? area = "test-area",
        string? sourcePack = null)
    {
        var room = new Room(id, name, "desc") { Area = area };
        if (sourcePack != null)
        {
            room.SetProperty(CommonProperties.SourcePack, sourcePack);
        }
        world.AddRoom(room);
        return room;
    }

    // ----- Cycle A: guards + dictionary re-index -----

    [Fact]
    public void RekeyRoom_ReindexesDictionary()
    {
        var world = new World();
        var room = AddRoom(world, "ns:old-key", "Room");

        var result = world.RekeyRoom("ns:old-key", "ns:gatehouse");

        result.Ok.Should().BeTrue();
        world.GetRoom("ns:old-key").Should().BeNull("the old key must be gone");
        world.GetRoom("ns:gatehouse").Should().BeSameAs(room, "the same room object resolves under the new key");
        room.Id.Should().Be("ns:gatehouse", "the room's own Id follows the dictionary key");
    }

    [Fact]
    public void RekeyRoom_MissingOldId_FailsAndChangesNothing()
    {
        var world = new World();
        AddRoom(world, "ns:exists", "Room");

        var result = world.RekeyRoom("ns:nope", "ns:new");

        result.Ok.Should().BeFalse();
        world.GetRoom("ns:exists").Should().NotBeNull();
        world.GetRoom("ns:new").Should().BeNull();
    }

    [Fact]
    public void RekeyRoom_TakenNewId_FailsAndChangesNothing()
    {
        var world = new World();
        var room = AddRoom(world, "ns:a", "A");
        AddRoom(world, "ns:b", "B");

        var result = world.RekeyRoom("ns:a", "ns:b");

        result.Ok.Should().BeFalse();
        room.Id.Should().Be("ns:a", "a failed rekey must not mutate the room");
        world.GetRoom("ns:a").Should().BeSameAs(room);
    }

    [Fact]
    public void RekeyRoom_NoReferencers_ReturnsEmptyLists()
    {
        var world = new World();
        AddRoom(world, "ns:lonely", "Lonely");

        var result = world.RekeyRoom("ns:lonely", "ns:renamed");

        result.Ok.Should().BeTrue();
        result.RetargetedRoomIds.Should().BeEmpty();
        result.EdgeReferences.Should().BeEmpty();
    }
}
