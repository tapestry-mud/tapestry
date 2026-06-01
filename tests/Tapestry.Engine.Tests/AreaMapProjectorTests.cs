using System;
using System.Linq;
using Tapestry.Engine;
using Tapestry.Engine.Mapping;
using Tapestry.Engine.Tags;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests;

public class AreaMapProjectorTests
{
    private static AreaMapProjector BuildProjector(World world)
    {
        var tags = new TagRegistry();
        tags.RegisterEngineTag("forest", "forest", new[] { EntityTypes.Room }, kind: "biome");
        return new AreaMapProjector(world, tags);
    }

    /// <summary>Wire a two-way exit between two rooms already added to the world.</summary>
    private static void Connect(World world, string fromId, Direction dir, string toId)
    {
        world.GetRoom(fromId)!.SetExit(dir, new Exit(toId));
        world.GetRoom(toId)!.SetExit(dir.Opposite(), new Exit(fromId));
    }

    private static Room AddRoom(World world, string id, string name, string area)
    {
        var room = new Room(id, name, "desc") { Area = area };
        world.AddRoom(room);
        return room;
    }

    [Fact]
    public void Single_room_area_projects_one_cell_at_origin()
    {
        var world = new World();
        var room = AddRoom(world, "castle:castle-0", "Castle Gate", "castle");

        var map = BuildProjector(world).Project(room, MapScope.WholeArea);

        Assert.Equal("castle", map.AreaId);
        Assert.Equal("castle:castle-0", map.RootRoomId);
        var cell = Assert.Single(map.Cells);
        Assert.Equal("castle:castle-0", cell.Id);
        Assert.Equal("Castle Gate", cell.Name);
        Assert.Equal(0, cell.X);
        Assert.Equal(0, cell.Y);
        Assert.Equal(0, cell.Z);
        Assert.Empty(cell.Exits);
        Assert.False(cell.HasVertical);
        Assert.False(cell.Collision);
        Assert.Empty(map.UnpositionedRoomIds);
    }

    [Fact]
    public void Straight_chain_lays_out_linearly_east()
    {
        var world = new World();
        AddRoom(world, "castle:castle-0", "A", "castle");
        AddRoom(world, "castle:castle-1", "B", "castle");
        AddRoom(world, "castle:castle-2", "C", "castle");
        Connect(world, "castle:castle-0", Direction.East, "castle:castle-1");
        Connect(world, "castle:castle-1", Direction.East, "castle:castle-2");

        var map = BuildProjector(world)
            .Project(world.GetRoom("castle:castle-0")!, MapScope.WholeArea);

        Assert.Equal(3, map.Cells.Count);
        var a = map.Cells.Single(c => c.Id == "castle:castle-0");
        var b = map.Cells.Single(c => c.Id == "castle:castle-1");
        var c2 = map.Cells.Single(c => c.Id == "castle:castle-2");
        Assert.Equal((0, 0), (a.X, a.Y));
        Assert.Equal((1, 0), (b.X, b.Y));
        Assert.Equal((2, 0), (c2.X, c2.Y));
        // exits are lowercase full names, sorted ordinal
        Assert.Equal(new[] { "east" }, a.Exits);
        Assert.Equal(new[] { "east", "west" }, b.Exits);
        Assert.Equal(new[] { "west" }, c2.Exits);
    }

    [Fact]
    public void North_and_south_move_y_not_x()
    {
        var world = new World();
        AddRoom(world, "ns:a", "A", "ns-area");
        AddRoom(world, "ns:b", "B", "ns-area");
        AddRoom(world, "ns:c", "C", "ns-area");
        Connect(world, "ns:a", Direction.North, "ns:b");
        Connect(world, "ns:a", Direction.South, "ns:c");

        var map = BuildProjector(world).Project(world.GetRoom("ns:a")!, MapScope.WholeArea);

        var b = map.Cells.Single(c => c.Id == "ns:b");
        var c2 = map.Cells.Single(c => c.Id == "ns:c");
        Assert.Equal((0, 1), (b.X, b.Y));
        Assert.Equal((0, -1), (c2.X, c2.Y));
    }
}
