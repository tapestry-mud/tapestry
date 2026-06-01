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

    // ----- Cycle B: exit retargeting + edge classification -----

    [Fact]
    public void RekeyRoom_RetargetsSameAreaDirectionalExits()
    {
        var world = new World();
        AddRoom(world, "ns:target", "Target");
        var neighbor = AddRoom(world, "ns:neighbor", "Neighbor");
        neighbor.SetExit(Direction.North, new Exit("ns:target"));

        var result = world.RekeyRoom("ns:target", "ns:gatehouse");

        result.Ok.Should().BeTrue();
        neighbor.GetExit(Direction.North)!.TargetRoomId.Should().Be("ns:gatehouse");
        result.RetargetedRoomIds.Should().ContainSingle().Which.Should().Be("ns:neighbor");
        result.EdgeReferences.Should().BeEmpty();
    }

    [Fact]
    public void RekeyRoom_RetargetsSameAreaKeywordExits()
    {
        var world = new World();
        AddRoom(world, "ns:target", "Target");
        var neighbor = AddRoom(world, "ns:neighbor", "Neighbor");
        neighbor.SetKeywordExit("gate", new Exit("ns:target"));

        var result = world.RekeyRoom("ns:target", "ns:gatehouse");

        neighbor.GetKeywordExit("gate")!.TargetRoomId.Should().Be("ns:gatehouse");
        result.RetargetedRoomIds.Should().ContainSingle().Which.Should().Be("ns:neighbor");
    }

    [Fact]
    public void RekeyRoom_OtherAreaReferencer_BecomesEdge_AndIsUntouched()
    {
        var world = new World();
        AddRoom(world, "ns:target", "Target", area: "test-area");
        var outsider = AddRoom(world, "ns:outsider", "Outsider", area: "other-area");
        outsider.SetExit(Direction.West, new Exit("ns:target"));

        var result = world.RekeyRoom("ns:target", "ns:gatehouse");

        outsider.GetExit(Direction.West)!.TargetRoomId.Should().Be("ns:target",
            "edge references must never be touched");
        result.RetargetedRoomIds.Should().BeEmpty();
        var edge = result.EdgeReferences.Should().ContainSingle().Subject;
        edge.Id.Should().Be("ns:outsider");
        edge.Name.Should().Be("Outsider");
        edge.IsPackRoom.Should().BeFalse();
    }

    [Fact]
    public void RekeyRoom_PackRoomReferencer_BecomesEdge_WithIsPackRoom()
    {
        var world = new World();
        AddRoom(world, "ns:target", "Target", area: "test-area");
        // Same area string, but it's pack source — still an edge.
        var packRoom = AddRoom(world, "lf:cellar", "Cellar", area: "test-area", sourcePack: "legends-forgotten");
        packRoom.SetExit(Direction.Up, new Exit("ns:target"));

        var result = world.RekeyRoom("ns:target", "ns:gatehouse");

        packRoom.GetExit(Direction.Up)!.TargetRoomId.Should().Be("ns:target");
        var edge = result.EdgeReferences.Should().ContainSingle().Subject;
        edge.Id.Should().Be("lf:cellar");
        edge.IsPackRoom.Should().BeTrue();
    }

    // ----- Self-loop exits: the renamed room's own exits that target itself -----

    [Fact]
    public void RekeyRoom_RetargetsSelfLoopDirectionalExit()
    {
        var world = new World();
        var room = AddRoom(world, "ns:target", "Target");
        room.SetExit(Direction.North, new Exit("ns:target"));

        var result = world.RekeyRoom("ns:target", "ns:gatehouse");

        result.Ok.Should().BeTrue();
        room.GetExit(Direction.North)!.TargetRoomId.Should().Be("ns:gatehouse",
            "the renamed room's own self-referencing exit must follow the new id");
    }

    [Fact]
    public void RekeyRoom_RetargetsSelfLoopKeywordExit()
    {
        var world = new World();
        var room = AddRoom(world, "ns:target", "Target");
        room.SetKeywordExit("mirror", new Exit("ns:target"));

        var result = world.RekeyRoom("ns:target", "ns:gatehouse");

        result.Ok.Should().BeTrue();
        room.GetKeywordExit("mirror")!.TargetRoomId.Should().Be("ns:gatehouse");
    }

    // ----- Cycle C: entities standing in the room follow the id -----

    [Fact]
    public void RekeyRoom_RelocatesEntitiesStandingInTheRoom()
    {
        var world = new World();
        var room = AddRoom(world, "ns:target", "Target");
        var player = new Entity("player", "Builder");
        var item = new Entity("item", "Dropped Sword");
        room.AddEntity(player);
        room.AddEntity(item);

        var result = world.RekeyRoom("ns:target", "ns:gatehouse");

        result.Ok.Should().BeTrue();
        player.LocationRoomId.Should().Be("ns:gatehouse");
        item.LocationRoomId.Should().Be("ns:gatehouse");
        room.Entities.Should().HaveCount(2, "relocation must not remove entities from the room");
    }
}
