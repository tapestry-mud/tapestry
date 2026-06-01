using System;
using System.Collections.Generic;
using System.Linq;
using Tapestry.Engine.Mapping;
using Xunit;

namespace Tapestry.Engine.Tests;

public class AsciiMapRendererTests
{
    private static readonly AsciiMapRenderer Renderer = new();

    private static RoomCell Cell(
        string id, string name, int x, int y, int z = 0,
        string[]? exits = null, string[]? markers = null,
        bool hasVertical = false, bool collision = false)
    {
        return new RoomCell(
            id, name, x, y, z,
            exits ?? Array.Empty<string>(),
            markers ?? Array.Empty<string>(),
            hasVertical, collision);
    }

    private static AreaMap Map(params RoomCell[] cells)
    {
        return new AreaMap("castle", cells.Length > 0 ? cells[0].Id : "",
            cells, Array.Empty<string>());
    }

    [Fact]
    public void Empty_map_renders_nothing_to_map_message()
    {
        var result = Renderer.Render(Map(), new ViewOptions());

        Assert.Equal("There is nothing to map here.", result);
    }

    [Fact]
    public void Single_room_dot_mode_renders_current_marker()
    {
        var map = Map(Cell("castle:castle-0", "Gate", 0, 0));
        var opts = new ViewOptions
        {
            CurrentRoomId = "castle:castle-0",
            Label = LabelMode.Dot,
            ShowCurrent = true,
        };

        var result = Renderer.Render(map, opts);

        Assert.Equal("[*]", result);
    }

    [Fact]
    public void Single_room_dot_mode_without_current_renders_blank_cell()
    {
        var map = Map(Cell("castle:castle-0", "Gate", 0, 0));
        var opts = new ViewOptions { Label = LabelMode.Dot, ShowCurrent = false };

        var result = Renderer.Render(map, opts);

        Assert.Equal("[ ]", result);
    }

    [Fact]
    public void Empty_legend_glyph_value_is_ignored_not_crashing()
    {
        var cells = new[] { Cell("ns:a", "A", 0, 0, markers: new[] { "forest" }) };
        var map = new AreaMap("ns-area", "ns:a", cells, Array.Empty<string>());
        var opts = new ViewOptions
        {
            Label = LabelMode.Dot,
            ShowCurrent = false,
            Legend = new Dictionary<string, string> { ["forest"] = "" },
        };

        var result = Renderer.Render(map, opts);

        // Empty glyph value: marker can't render, cell falls back to blank, no legend, no crash.
        Assert.Equal("[ ]", result);
    }

    [Fact]
    public void Duplicate_cell_ids_keep_first_label()
    {
        // The model doesn't forbid duplicate ids; the renderer's contract is first-seen-wins
        // (same as position dedup). Both occurrences resolve to the first label.
        var cells = new[]
        {
            Cell("ns:a", "A", 0, 0),
            Cell("ns:a", "A again", 1, 0),
        };
        var map = new AreaMap("ns-area", "ns:a", cells, Array.Empty<string>());
        var opts = new ViewOptions { Label = LabelMode.Id, ShowCurrent = false };

        var lines = Renderer.Render(map, opts).Split("\r\n");

        // Both cells render with label '1' (first-seen id wins the label assignment).
        Assert.Equal("[1] [1]", lines[0]);
    }

    // -------------------------------------------------------------------------
    // Shared fixture — spec §4.1 4-room box
    // -------------------------------------------------------------------------

    /// <summary>The spec §4.1 4-room box: Gate(0,0)-East Wall(1,0) / West Wall(0,-1)-Courtyard(1,-1).</summary>
    private static AreaMap BoxMap()
    {
        var cells = new[]
        {
            Cell("castle:castle-0", "Castle Gate", 0, 0, exits: new[] { "east", "south" }),
            Cell("castle:castle-1", "East Wall", 1, 0, exits: new[] { "south", "west" }),
            Cell("castle:castle-2", "Courtyard", 1, -1, exits: new[] { "north", "west" }),
            Cell("castle:castle-3", "West Wall", 0, -1, exits: new[] { "east", "north" }),
        };
        return new AreaMap("castle", "castle:castle-0", cells, Array.Empty<string>());
    }

    // -------------------------------------------------------------------------
    // Group A — grid placement + reciprocal connectors (plan Task 9)
    // -------------------------------------------------------------------------

    [Fact]
    public void Box_dot_mode_renders_square_with_connectors()
    {
        var opts = new ViewOptions
        {
            CurrentRoomId = "castle:castle-0",
            Label = LabelMode.Dot,
            ShowCurrent = true,
        };

        var lines = Renderer.Render(BoxMap(), opts).Split("\r\n");

        Assert.Equal("[*]-[ ]", lines[0]);
        Assert.Equal(" |   |", lines[1]);
        Assert.Equal("[ ]-[ ]", lines[2]);
    }

    [Fact]
    public void One_way_exit_draws_no_connector()
    {
        // A has east exit to B; B has no west exit back -> no '-' between them.
        var cells = new[]
        {
            Cell("ns:a", "A", 0, 0, exits: new[] { "east" }),
            Cell("ns:b", "B", 1, 0, exits: Array.Empty<string>()),
        };
        var map = new AreaMap("ns-area", "ns:a", cells, Array.Empty<string>());
        var opts = new ViewOptions { Label = LabelMode.Dot, ShowCurrent = false };

        var lines = Renderer.Render(map, opts).Split("\r\n");

        Assert.Equal("[ ] [ ]", lines[0]);
    }

    [Fact]
    public void North_neighbor_renders_above()
    {
        var cells = new[]
        {
            Cell("ns:a", "A", 0, 0, exits: new[] { "north" }),
            Cell("ns:b", "B", 0, 1, exits: new[] { "south" }),
        };
        var map = new AreaMap("ns-area", "ns:a", cells, Array.Empty<string>());
        var opts = new ViewOptions { Label = LabelMode.Dot, ShowCurrent = false };

        var lines = Renderer.Render(map, opts).Split("\r\n");

        // B (y=1) is on top, A (y=0) below, with a vertical connector between.
        Assert.Equal("[ ]", lines[0]);
        Assert.Equal(" |", lines[1]);
        Assert.Equal("[ ]", lines[2]);
    }

    // -------------------------------------------------------------------------
    // Group B — markers, legend priority, current override, vertical glyphs (plan Task 10)
    // -------------------------------------------------------------------------

    [Fact]
    public void Marker_renders_legend_glyph_in_dot_mode()
    {
        var cells = new[] { Cell("ns:a", "A", 0, 0, markers: new[] { "forest" }) };
        var map = new AreaMap("ns-area", "ns:a", cells, Array.Empty<string>());
        var opts = new ViewOptions
        {
            Label = LabelMode.Dot,
            ShowCurrent = false,
            Legend = new Dictionary<string, string> { ["forest"] = "f" },
        };

        var result = Renderer.Render(map, opts);

        var lines = result.Split("\r\n");
        Assert.Equal("[f]", lines[0]);
        // and the glyph legend explains it
        Assert.Contains(" f = forest", lines);
    }

    [Fact]
    public void First_marker_with_legend_entry_wins()
    {
        // Markers are ordered; "water" has no legend entry, "forest" does -> 'f'.
        var cells = new[] { Cell("ns:a", "A", 0, 0, markers: new[] { "water", "forest" }) };
        var map = new AreaMap("ns-area", "ns:a", cells, Array.Empty<string>());
        var opts = new ViewOptions
        {
            Label = LabelMode.Dot,
            ShowCurrent = false,
            Legend = new Dictionary<string, string> { ["forest"] = "f" },
        };

        var lines = Renderer.Render(map, opts).Split("\r\n");

        Assert.Equal("[f]", lines[0]);
    }

    [Fact]
    public void Current_room_overrides_marker_glyph()
    {
        var cells = new[] { Cell("ns:a", "A", 0, 0, markers: new[] { "forest" }) };
        var map = new AreaMap("ns-area", "ns:a", cells, Array.Empty<string>());
        var opts = new ViewOptions
        {
            CurrentRoomId = "ns:a",
            Label = LabelMode.Dot,
            ShowCurrent = true,
            Legend = new Dictionary<string, string> { ["forest"] = "f" },
        };

        var lines = Renderer.Render(map, opts).Split("\r\n");

        Assert.Equal("[*]", lines[0]);
    }

    [Fact]
    public void Vertical_room_renders_up_down_glyph_in_dot_mode()
    {
        var cells = new[]
        {
            Cell("ns:up", "Up Only", 0, 0, exits: new[] { "up" }, hasVertical: true),
            Cell("ns:down", "Down Only", 1, 0, exits: new[] { "down" }, hasVertical: true),
            Cell("ns:both", "Both", 2, 0, exits: new[] { "down", "up" }, hasVertical: true),
        };
        var map = new AreaMap("ns-area", "ns:up", cells, Array.Empty<string>());
        var opts = new ViewOptions { Label = LabelMode.Dot, ShowCurrent = false };

        var lines = Renderer.Render(map, opts).Split("\r\n");

        Assert.Equal("[^] [v] [%]", lines[0]);
    }

    // -------------------------------------------------------------------------
    // Group C — Id and Name label modes (plan Task 11)
    // -------------------------------------------------------------------------

    [Fact]
    public void Id_mode_renders_indexed_cells_and_id_legend()
    {
        var opts = new ViewOptions
        {
            CurrentRoomId = "castle:castle-0",
            Label = LabelMode.Id,
            ShowCurrent = true,
        };

        var lines = Renderer.Render(BoxMap(), opts).Split("\r\n");

        // Indexed grid; current room gets * brackets. Cells are labelled in
        // AreaMap.Cells order: castle-0=1, castle-1=2, castle-2=3, castle-3=4.
        Assert.Equal("*1*-[2]", lines[0]);
        Assert.Equal(" |   |", lines[1]);
        Assert.Equal("[4]-[3]", lines[2]);
        // Blank separator then the legend (this IS the spec's rooms table):
        Assert.Equal("", lines[3]);
        Assert.Equal(" 1) castle-0  Castle Gate  (east, south)  <- you", lines[4]);
        Assert.Equal(" 2) castle-1  East Wall  (south, west)", lines[5]);
        Assert.Equal(" 3) castle-2  Courtyard  (north, west)", lines[6]);
        Assert.Equal(" 4) castle-3  West Wall  (east, north)", lines[7]);
    }

    [Fact]
    public void Id_mode_legend_includes_markers()
    {
        var cells = new[]
        {
            Cell("ns:a", "Glade", 0, 0, markers: new[] { "forest" }),
        };
        var map = new AreaMap("ns-area", "ns:a", cells, Array.Empty<string>());
        var opts = new ViewOptions { Label = LabelMode.Id, ShowCurrent = false };

        var lines = Renderer.Render(map, opts).Split("\r\n");

        Assert.Equal(" 1) a  Glade  ()  [forest]", lines[2]);
    }

    [Fact]
    public void Name_mode_legend_has_names_but_no_ids()
    {
        var opts = new ViewOptions
        {
            CurrentRoomId = "castle:castle-0",
            Label = LabelMode.Name,
            ShowCurrent = true,
        };

        var result = Renderer.Render(BoxMap(), opts);

        Assert.Contains(" 1) Castle Gate  <- you", result);
        Assert.Contains(" 2) East Wall", result);
        // No ids anywhere in name mode output:
        Assert.DoesNotContain("castle-0", result);
        Assert.DoesNotContain("castle-1", result);
    }

    [Fact]
    public void Dot_mode_output_contains_no_ids()
    {
        var opts = new ViewOptions
        {
            CurrentRoomId = "castle:castle-0",
            Label = LabelMode.Dot,
            ShowCurrent = true,
        };

        var result = Renderer.Render(BoxMap(), opts);

        // The player map must never leak room ids.
        Assert.DoesNotContain("castle-", result);
    }

    // -------------------------------------------------------------------------
    // Group D — collision, unpositioned, off-plane footnotes (plan Task 12)
    // -------------------------------------------------------------------------

    [Fact]
    public void Overlapping_rooms_first_renders_rest_footnoted()
    {
        var cells = new[]
        {
            Cell("ns:a", "A", 0, 0, collision: true),
            Cell("ns:b", "B", 0, 0, collision: true), // same position -> not drawn
        };
        var map = new AreaMap("ns-area", "ns:a", cells, Array.Empty<string>());
        var opts = new ViewOptions { Label = LabelMode.Dot, ShowCurrent = false };

        var result = Renderer.Render(map, opts);

        var lines = result.Split("\r\n");
        Assert.Equal("[ ]", lines[0]); // only one cell drawn
        Assert.Contains(
            " ! 1 room(s) overlap others (non-grid layout) and are not drawn: b",
            lines);
    }

    [Fact]
    public void Unpositioned_rooms_are_footnoted()
    {
        var cells = new[] { Cell("ns:a", "A", 0, 0) };
        var map = new AreaMap("ns-area", "ns:a", cells, new[] { "ns:island" });
        var opts = new ViewOptions { Label = LabelMode.Dot, ShowCurrent = false };

        var result = Renderer.Render(map, opts);

        Assert.Contains(" ! not connected to the map: island", result.Split("\r\n"));
    }

    [Fact]
    public void Off_plane_rooms_are_footnoted_not_drawn()
    {
        var cells = new[]
        {
            Cell("ns:a", "A", 0, 0, z: 0),
            Cell("ns:tower", "Tower", 0, 0, z: 1),
        };
        var map = new AreaMap("ns-area", "ns:a", cells, Array.Empty<string>());
        var opts = new ViewOptions { Label = LabelMode.Dot, ShowCurrent = false, Plane = 0 };

        var result = Renderer.Render(map, opts);

        var lines = result.Split("\r\n");
        Assert.Equal("[ ]", lines[0]);
        Assert.Contains(" ! 1 room(s) above or below this level are not shown", lines);
    }

    [Fact]
    public void Map_with_only_off_plane_cells_reports_nothing_on_this_level()
    {
        var cells = new[] { Cell("ns:tower", "Tower", 0, 0, z: 1) };
        var map = new AreaMap("ns-area", "ns:tower", cells, Array.Empty<string>());
        var opts = new ViewOptions { Label = LabelMode.Dot, Plane = 0 };

        var result = Renderer.Render(map, opts);

        Assert.Equal("There is nothing to map on this level.", result);
    }
}
