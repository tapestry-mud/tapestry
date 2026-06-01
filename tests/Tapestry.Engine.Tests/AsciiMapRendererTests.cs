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
}
