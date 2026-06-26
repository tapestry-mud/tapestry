using Tapestry.Engine;
using Tapestry.Engine.Consequence;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests.Consequence;

public class ConsequenceOverlayTests
{
    private static (World world, EventBus bus, ConsequenceOverlay overlay) Build()
    {
        var world = new World();
        var bus = new EventBus();
        var overlay = new ConsequenceOverlay(world, bus);
        return (world, bus, overlay);
    }

    private static Room AddRoom(World world, string id, string area)
    {
        var room = new Room(id, "Room " + id, "A room.") { Area = area };
        world.AddRoom(room);
        return room;
    }

    [Fact]
    public void Stamp_then_List_returns_the_entry()
    {
        var (world, _, overlay) = Build();
        AddRoom(world, "u:area-0_0_0", "u:area");
        overlay.Stamp("u:area-0_0_0", "looted", "ephemeral");

        var list = overlay.List("u:area-0_0_0");
        Assert.Single(list);
        Assert.Equal("looted", list[0].Kind);
        Assert.Equal("ephemeral", list[0].Lifespan);
        Assert.True(overlay.Has("u:area-0_0_0", "looted"));
    }

    [Fact]
    public void Stamp_is_idempotent_on_room_and_kind()
    {
        var (world, _, overlay) = Build();
        AddRoom(world, "u:area-0_0_0", "u:area");
        overlay.Stamp("u:area-0_0_0", "looted", "ephemeral");
        overlay.Stamp("u:area-0_0_0", "looted", "persistent"); // re-stamp: last lifespan wins, no dup

        var list = overlay.List("u:area-0_0_0");
        Assert.Single(list);
        Assert.Equal("persistent", list[0].Lifespan);
    }

    [Fact]
    public void AreaTick_evicts_ephemeral_but_keeps_persistent_and_succession()
    {
        var (world, bus, overlay) = Build();
        AddRoom(world, "u:area-0_0_0", "u:area");
        overlay.Stamp("u:area-0_0_0", "looted", "ephemeral");
        overlay.Stamp("u:area-0_0_0", "boss-slain", "persistent");
        overlay.Stamp("u:area-0_0_0", "collapsed", "succession-seed");

        bus.Publish(new GameEvent { Type = "area.tick", Data = new Dictionary<string, object?> { ["areaId"] = "u:area" } });

        Assert.False(overlay.Has("u:area-0_0_0", "looted"));      // ephemeral evicted on repop
        Assert.True(overlay.Has("u:area-0_0_0", "boss-slain"));   // persistent survives
        Assert.True(overlay.Has("u:area-0_0_0", "collapsed"));    // succession-seed survives (tag-only)
    }

    [Fact]
    public void AreaTick_only_evicts_the_ticking_area()
    {
        var (world, bus, overlay) = Build();
        AddRoom(world, "u:area-0_0_0", "u:area");
        AddRoom(world, "u:other-0_0_0", "u:other");
        overlay.Stamp("u:area-0_0_0", "looted", "ephemeral");
        overlay.Stamp("u:other-0_0_0", "looted", "ephemeral");

        bus.Publish(new GameEvent { Type = "area.tick", Data = new Dictionary<string, object?> { ["areaId"] = "u:area" } });

        Assert.False(overlay.Has("u:area-0_0_0", "looted"));
        Assert.True(overlay.Has("u:other-0_0_0", "looted")); // untouched area keeps its ephemeral
    }

    [Fact]
    public void ClearRoom_removes_all_entries()
    {
        var (world, _, overlay) = Build();
        AddRoom(world, "u:area-0_0_0", "u:area");
        overlay.Stamp("u:area-0_0_0", "looted", "ephemeral");
        Assert.True(overlay.ClearRoom("u:area-0_0_0"));
        Assert.Empty(overlay.List("u:area-0_0_0"));
    }

    [Fact]
    public void CollapseExit_removes_the_exit_from_the_live_graph_and_records_it()
    {
        var (world, _, overlay) = Build();
        var room = AddRoom(world, "u:area-0_0_0", "u:area");
        room.SetExit(Direction.North, new Exit("u:area-0_1_0"));
        Assert.NotNull(room.GetExit(Direction.North));

        var ok = overlay.CollapseExit("u:area-0_0_0", "north", "collapsed", "succession-seed");

        Assert.True(ok);
        Assert.Null(room.GetExit(Direction.North));                  // exit gone from the live graph
        Assert.True(overlay.Has("u:area-0_0_0", "collapsed"));       // recorded
        var entry = overlay.List("u:area-0_0_0").Single(e => e.Kind == "collapsed");
        Assert.Equal("succession-seed", entry.Lifespan);             // succession-seed (survives repop, tag-only)
    }

    [Fact]
    public void CollapseExit_records_the_caller_supplied_kind_not_a_hardcoded_one()
    {
        // The engine must stay content-agnostic: CollapseExit records whatever opaque
        // kind/lifespan the caller passes, never a baked-in content string.
        var (world, _, overlay) = Build();
        var room = AddRoom(world, "u:area-0_0_0", "u:area");
        room.SetExit(Direction.North, new Exit("u:area-0_1_0"));

        var ok = overlay.CollapseExit("u:area-0_0_0", "north", "sealed", "persistent");

        Assert.True(ok);
        Assert.Null(room.GetExit(Direction.North));
        Assert.True(overlay.Has("u:area-0_0_0", "sealed"));
        Assert.False(overlay.Has("u:area-0_0_0", "collapsed"));   // no baked-in kind
        Assert.Equal("persistent", overlay.List("u:area-0_0_0").Single(e => e.Kind == "sealed").Lifespan);
    }

    [Fact]
    public void CollapseExit_returns_false_for_a_bad_room_or_direction()
    {
        var (world, _, overlay) = Build();
        AddRoom(world, "u:area-0_0_0", "u:area");
        Assert.False(overlay.CollapseExit("u:nope-9_9_9", "north", "collapsed", "succession-seed"));
        Assert.False(overlay.CollapseExit("u:area-0_0_0", "sideways", "collapsed", "succession-seed"));
    }

    [Fact]
    public void CollapseExit_survives_an_area_tick_then_clears_on_reboot()
    {
        // Reboot is modeled by a fresh overlay (in-memory, starts empty).
        var (world, bus, overlay) = Build();
        var room = AddRoom(world, "u:area-0_0_0", "u:area");
        room.SetExit(Direction.North, new Exit("u:area-0_1_0"));
        overlay.CollapseExit("u:area-0_0_0", "north", "collapsed", "succession-seed");

        bus.Publish(new GameEvent { Type = "area.tick", Data = new Dictionary<string, object?> { ["areaId"] = "u:area" } });
        Assert.True(overlay.Has("u:area-0_0_0", "collapsed")); // succession-seed survives the repop tick

        var rebooted = new ConsequenceOverlay(world, bus);
        Assert.False(rebooted.Has("u:area-0_0_0", "collapsed")); // a fresh overlay is empty (reboot drops it)
    }
}
