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
    public void Null_area_rooms_do_not_join_another_areas_map()
    {
        var world = new World();
        AddRoom(world, "castle:castle-0", "Gate", "castle");
        // A room with NO area (orphaned/staging room) — must not appear in castle's map.
        world.AddRoom(new Room("orphan:lost", "Lost Room", "desc"));

        var map = BuildProjector(world)
            .Project(world.GetRoom("castle:castle-0")!, MapScope.WholeArea);

        var cell = Assert.Single(map.Cells);
        Assert.Equal("castle:castle-0", cell.Id);
        Assert.Empty(map.UnpositionedRoomIds);
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

    // ── Batch 1 (Task 3): box closure + first-reach-wins ─────────────────────

    [Fact]
    public void Four_room_box_closes_onto_clean_cells()
    {
        // A -e-> B
        // |s          (dig pattern from the spec §4.1 example)
        // D <-w- C
        var world = new World();
        AddRoom(world, "castle:castle-0", "A", "castle");
        AddRoom(world, "castle:castle-1", "B", "castle");
        AddRoom(world, "castle:castle-2", "C", "castle");
        AddRoom(world, "castle:castle-3", "D", "castle");
        Connect(world, "castle:castle-0", Direction.East, "castle:castle-1");
        Connect(world, "castle:castle-1", Direction.South, "castle:castle-2");
        Connect(world, "castle:castle-2", Direction.West, "castle:castle-3");
        Connect(world, "castle:castle-3", Direction.North, "castle:castle-0");

        var map = BuildProjector(world)
            .Project(world.GetRoom("castle:castle-0")!, MapScope.WholeArea);

        Assert.Equal(4, map.Cells.Count);
        var a = map.Cells.Single(c => c.Id == "castle:castle-0");
        var b = map.Cells.Single(c => c.Id == "castle:castle-1");
        var c2 = map.Cells.Single(c => c.Id == "castle:castle-2");
        var d = map.Cells.Single(c => c.Id == "castle:castle-3");
        Assert.Equal((0, 0), (a.X, a.Y));
        Assert.Equal((1, 0), (b.X, b.Y));
        Assert.Equal((1, -1), (c2.X, c2.Y));
        Assert.Equal((0, -1), (d.X, d.Y));
        // The loop closes cleanly: no collisions anywhere.
        Assert.All(map.Cells, c => Assert.False(c.Collision));
    }

    [Fact]
    public void Room_first_reached_wins_its_cell()
    {
        // BFS from A: exits are iterated in enum order (North=0, South=1, East=2, West=3),
        // so A's South->D is expanded before East->B. D-e->C places C at (1,-1) first.
        // When B is later dequeued, B-s->C finds C already positioned (first-reach-wins)
        // and skips it. No collision -- C stays at (1,-1).
        var world = new World();
        AddRoom(world, "ns:a", "A", "ns-area");
        AddRoom(world, "ns:b", "B", "ns-area");
        AddRoom(world, "ns:c", "C", "ns-area");
        AddRoom(world, "ns:d", "D", "ns-area");
        Connect(world, "ns:a", Direction.East, "ns:b");
        Connect(world, "ns:b", Direction.South, "ns:c");
        Connect(world, "ns:a", Direction.South, "ns:d");
        Connect(world, "ns:d", Direction.East, "ns:c");

        var map = BuildProjector(world).Project(world.GetRoom("ns:a")!, MapScope.WholeArea);

        var c2 = map.Cells.Single(c => c.Id == "ns:c");
        Assert.Equal((1, -1), (c2.X, c2.Y));
        Assert.All(map.Cells, c => Assert.False(c.Collision));
    }

    // ── Batch 2 (Task 4): one-way exit + forced collision ────────────────────

    [Fact]
    public void One_way_exit_projects_target_without_crashing()
    {
        // A has an east exit to B, but B has NO west exit back.
        var world = new World();
        AddRoom(world, "ns:a", "A", "ns-area");
        AddRoom(world, "ns:b", "B", "ns-area");
        world.GetRoom("ns:a")!.SetExit(Direction.East, new Exit("ns:b"));

        var map = BuildProjector(world).Project(world.GetRoom("ns:a")!, MapScope.WholeArea);

        Assert.Equal(2, map.Cells.Count);
        var b = map.Cells.Single(c => c.Id == "ns:b");
        Assert.Equal((1, 0), (b.X, b.Y));
        Assert.Empty(b.Exits); // B genuinely has no exits — the renderer draws no connector
    }

    [Fact]
    public void Two_rooms_forced_onto_same_cell_both_flag_collision()
    {
        // Non-Euclidean diamond: A(0,0), B(1,0) via east, C(0,1) via north.
        // Then C-e->E puts E at (1,1), and B-n->F puts F at (1,1) too -> E and F collide.
        var world = new World();
        AddRoom(world, "ns:a", "A", "ns-area");
        AddRoom(world, "ns:b", "B", "ns-area");
        AddRoom(world, "ns:c", "C", "ns-area");
        AddRoom(world, "ns:e", "E", "ns-area");
        AddRoom(world, "ns:f", "F", "ns-area");
        Connect(world, "ns:a", Direction.East, "ns:b");
        Connect(world, "ns:a", Direction.North, "ns:c");
        Connect(world, "ns:c", Direction.East, "ns:e");  // E at (1,1)
        Connect(world, "ns:b", Direction.North, "ns:f"); // F also at (1,1) -> collision

        var map = BuildProjector(world).Project(world.GetRoom("ns:a")!, MapScope.WholeArea);

        var e = map.Cells.Single(c => c.Id == "ns:e");
        var f = map.Cells.Single(c => c.Id == "ns:f");
        Assert.True(e.Collision);
        Assert.True(f.Collision);
        Assert.Equal((e.X, e.Y), (f.X, f.Y));
        // Non-colliding rooms stay clean.
        Assert.False(map.Cells.Single(c => c.Id == "ns:a").Collision);
        Assert.False(map.Cells.Single(c => c.Id == "ns:b").Collision);
    }

    // ── Batch 3 (Task 5): vertical exits + radius scope ──────────────────────

    [Fact]
    public void Up_exit_flags_vertical_and_moves_only_z()
    {
        var world = new World();
        AddRoom(world, "ns:a", "A", "ns-area");
        AddRoom(world, "ns:tower", "Tower", "ns-area");
        Connect(world, "ns:a", Direction.Up, "ns:tower");

        var map = BuildProjector(world).Project(world.GetRoom("ns:a")!, MapScope.WholeArea);

        var a = map.Cells.Single(c => c.Id == "ns:a");
        var tower = map.Cells.Single(c => c.Id == "ns:tower");
        Assert.True(a.HasVertical);
        Assert.True(tower.HasVertical);
        Assert.Equal((0, 0, 0), (a.X, a.Y, a.Z));
        Assert.Equal((0, 0, 1), (tower.X, tower.Y, tower.Z));
        // x/y unchanged, z is the only delta; no collision because z differs.
        Assert.False(tower.Collision);
    }

    [Fact]
    public void Radius_scope_bounds_the_bfs()
    {
        // Chain of 5: r0 -e-> r1 -e-> r2 -e-> r3 -e-> r4. Radius(2) from r0 => r0, r1, r2 only.
        var world = new World();
        AddRoom(world, "ns:r0", "R0", "ns-area");
        AddRoom(world, "ns:r1", "R1", "ns-area");
        AddRoom(world, "ns:r2", "R2", "ns-area");
        AddRoom(world, "ns:r3", "R3", "ns-area");
        AddRoom(world, "ns:r4", "R4", "ns-area");
        Connect(world, "ns:r0", Direction.East, "ns:r1");
        Connect(world, "ns:r1", Direction.East, "ns:r2");
        Connect(world, "ns:r2", Direction.East, "ns:r3");
        Connect(world, "ns:r3", Direction.East, "ns:r4");

        var map = BuildProjector(world).Project(world.GetRoom("ns:r0")!, MapScope.Radius(2));

        Assert.Equal(3, map.Cells.Count);
        Assert.Contains(map.Cells, c => c.Id == "ns:r0");
        Assert.Contains(map.Cells, c => c.Id == "ns:r1");
        Assert.Contains(map.Cells, c => c.Id == "ns:r2");
        Assert.DoesNotContain(map.Cells, c => c.Id == "ns:r3");
        // Radius scope roots at the PASSED room, not the lowest id.
        Assert.Equal("ns:r0", map.RootRoomId);
        // Radius scope reports no unpositioned rooms (that concept is whole-area only).
        Assert.Empty(map.UnpositionedRoomIds);
    }

    [Fact]
    public void Radius_scope_centered_mid_chain_roots_at_the_player()
    {
        var world = new World();
        AddRoom(world, "ns:r0", "R0", "ns-area");
        AddRoom(world, "ns:r1", "R1", "ns-area");
        AddRoom(world, "ns:r2", "R2", "ns-area");
        Connect(world, "ns:r0", Direction.East, "ns:r1");
        Connect(world, "ns:r1", Direction.East, "ns:r2");

        var map = BuildProjector(world).Project(world.GetRoom("ns:r1")!, MapScope.Radius(1));

        Assert.Equal("ns:r1", map.RootRoomId);
        var r1 = map.Cells.Single(c => c.Id == "ns:r1");
        Assert.Equal((0, 0), (r1.X, r1.Y));
        var r0 = map.Cells.Single(c => c.Id == "ns:r0");
        Assert.Equal((-1, 0), (r0.X, r0.Y));
        var r2 = map.Cells.Single(c => c.Id == "ns:r2");
        Assert.Equal((1, 0), (r2.X, r2.Y));
        Assert.Equal(3, map.Cells.Count);
    }

    // ── Batch 4 (Task 6): whole-area determinism + area filter + unpositioned ─

    [Fact]
    public void Whole_area_roots_deterministically_regardless_of_passed_root()
    {
        var world = new World();
        AddRoom(world, "castle:castle-0", "A", "castle");
        AddRoom(world, "castle:castle-1", "B", "castle");
        Connect(world, "castle:castle-0", Direction.East, "castle:castle-1");

        var projector = BuildProjector(world);
        var fromA = projector.Project(world.GetRoom("castle:castle-0")!, MapScope.WholeArea);
        var fromB = projector.Project(world.GetRoom("castle:castle-1")!, MapScope.WholeArea);

        // Same root (lowest id) and identical cell positions either way.
        Assert.Equal("castle:castle-0", fromA.RootRoomId);
        Assert.Equal("castle:castle-0", fromB.RootRoomId);
        Assert.Equal(
            fromA.Cells.Select(c => (c.Id, c.X, c.Y, c.Z)),
            fromB.Cells.Select(c => (c.Id, c.X, c.Y, c.Z)));
    }

    [Fact]
    public void Whole_area_excludes_rooms_from_other_areas()
    {
        var world = new World();
        AddRoom(world, "castle:castle-0", "Gate", "castle");
        AddRoom(world, "forest:forest-0", "Glade", "forest");
        // Cross-area exit (e.g. wired by 'link'):
        Connect(world, "castle:castle-0", Direction.East, "forest:forest-0");

        var map = BuildProjector(world)
            .Project(world.GetRoom("castle:castle-0")!, MapScope.WholeArea);

        // The forest room is reachable but in another area — whole-area scope skips it.
        var cell = Assert.Single(map.Cells);
        Assert.Equal("castle:castle-0", cell.Id);
    }

    [Fact]
    public void Whole_area_lists_disconnected_rooms_as_unpositioned()
    {
        var world = new World();
        AddRoom(world, "castle:castle-0", "Gate", "castle");
        AddRoom(world, "castle:castle-1", "Wall", "castle");
        AddRoom(world, "castle:island", "Island", "castle"); // no exits at all
        Connect(world, "castle:castle-0", Direction.East, "castle:castle-1");

        var map = BuildProjector(world)
            .Project(world.GetRoom("castle:castle-0")!, MapScope.WholeArea);

        Assert.Equal(2, map.Cells.Count);
        Assert.Equal(new[] { "castle:island" }, map.UnpositionedRoomIds);
    }

    // ── Batch 5 (Task 7): terrain + biome markers ────────────────────────────

    [Fact]
    public void Terrain_property_and_biome_tag_populate_markers()
    {
        var world = new World();
        var room = AddRoom(world, "ns:a", "A", "ns-area");
        room.SetProperty("terrain", "stone");
        room.AddTag("forest"); // registered as a biome tag in BuildProjector

        var map = BuildProjector(world).Project(room, MapScope.WholeArea);

        var cell = Assert.Single(map.Cells);
        Assert.Equal(new[] { "stone", "forest" }, cell.Markers);
    }

    [Fact]
    public void Room_without_terrain_or_biome_has_no_markers()
    {
        var world = new World();
        var room = AddRoom(world, "ns:a", "A", "ns-area");
        room.AddTag("safe"); // not a biome-kind tag

        var map = BuildProjector(world).Project(room, MapScope.WholeArea);

        Assert.Empty(Assert.Single(map.Cells).Markers);
    }

    [Fact]
    public void Duplicate_terrain_and_biome_value_appears_once()
    {
        var world = new World();
        var room = AddRoom(world, "ns:a", "A", "ns-area");
        room.SetProperty("terrain", "forest");
        room.AddTag("forest");

        var map = BuildProjector(world).Project(room, MapScope.WholeArea);

        Assert.Equal(new[] { "forest" }, Assert.Single(map.Cells).Markers);
    }
}
