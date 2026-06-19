// tests/Tapestry.Scripting.Tests/WorldMapJsSurfaceTests.cs
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Scripting;
using Tapestry.Shared;

namespace Tapestry.Scripting.Tests;

/// <summary>
/// End-to-end JS surface tests for tapestry.world.renderAreaMap and
/// tapestry.world.projectArea. Drives through the real JintRuntime + DI stack.
/// </summary>
public class WorldMapJsSurfaceTests
{
    /// <summary>
    /// Build a real JintRuntime using the full DI stack (AddTapestryScripting),
    /// so WorldModule gets all its deps — including AreaMapProjector + AsciiMapRenderer
    /// — via normal registration. Returns both the runtime and the World.
    /// </summary>
    private static (JintRuntime rt, World world) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<World>());
    }

    /// <summary>
    /// Build a 4-room box (castle-0 -- E -- castle-1
    ///                                      |
    ///                      castle-3 -- E -- castle-2)
    /// with reciprocal exits so the projector can lay them out on a 2×2 grid.
    /// </summary>
    private static void AddCastleBox(World world)
    {
        var r0 = new Room("castle:castle-0", "Castle Gate", "The main gate.") { Area = "castle" };
        var r1 = new Room("castle:castle-1", "East Tower", "The east tower.") { Area = "castle" };
        var r2 = new Room("castle:castle-2", "South East", "The south-east corner.") { Area = "castle" };
        var r3 = new Room("castle:castle-3", "South Gate", "The south gate.") { Area = "castle" };

        // castle-0 <-> east <-> castle-1 (top edge)
        r0.SetExit(Direction.East, new Exit("castle:castle-1"));
        r1.SetExit(Direction.West, new Exit("castle:castle-0"));

        // castle-1 <-> south <-> castle-2 (right edge)
        r1.SetExit(Direction.South, new Exit("castle:castle-2"));
        r2.SetExit(Direction.North, new Exit("castle:castle-1"));

        // castle-2 <-> west <-> castle-3 (bottom edge)
        r2.SetExit(Direction.West, new Exit("castle:castle-3"));
        r3.SetExit(Direction.East, new Exit("castle:castle-2"));

        // castle-3 <-> north <-> castle-0 (left edge)
        r3.SetExit(Direction.North, new Exit("castle:castle-0"));
        r0.SetExit(Direction.South, new Exit("castle:castle-3"));

        world.AddRoom(r0);
        world.AddRoom(r1);
        world.AddRoom(r2);
        world.AddRoom(r3);
    }

    /// <summary>Add a fifth room one level above castle-1 (Up/Down exits) so the
    /// area has a non-zero z-plane.</summary>
    private static void AddTowerAbove(World world)
    {
        var tower = new Room("castle:tower-top", "Tower Top", "The top of the tower.") { Area = "castle" };
        tower.SetExit(Direction.Down, new Exit("castle:castle-1"));
        world.GetRoom("castle:castle-1")!.SetExit(Direction.Up, new Exit("castle:tower-top"));
        world.AddRoom(tower);
    }

    // -----------------------------------------------------------------------
    // Test 1: renderAreaMap returns ASCII that contains room id + name + current marker
    // -----------------------------------------------------------------------

    [Fact]
    public void RenderAreaMap_ReturnsAscii_ContainingRoomIdNameAndCurrentMarker()
    {
        var (rt, world) = BuildRuntime();
        AddCastleBox(world);

        // scope: 'area' → WholeArea projection; label: 'id' → legend with short ids;
        // showCurrent: true → * brackets around the current room's label index.
        var result = EsmTest.Eval(rt, """
            tapestry.world.renderAreaMap('castle:castle-0', {
                scope: 'area',
                label: 'id',
                showCurrent: true
            });
            """);

        var text = result?.ToString() ?? "";
        text.Should().Contain("castle-0",
            "the legend entry for the root room should include its short id");
        text.Should().Contain("Castle Gate",
            "the legend entry for the root room should include its name");
        text.Should().Contain("*1*",
            "the current room is marked with * brackets and its label index '1'");
    }

    // -----------------------------------------------------------------------
    // Test 2: renderAreaMap on an unknown room returns the sentinel string
    // -----------------------------------------------------------------------

    [Fact]
    public void RenderAreaMap_UnknownRoom_ReturnsSentinelString()
    {
        var (rt, _) = BuildRuntime();

        var result = EsmTest.Eval(rt, """
            tapestry.world.renderAreaMap('nope:missing', { scope: 'area' });
            """);

        result?.ToString().Should().Be("There is nothing to map here.");
    }

    // -----------------------------------------------------------------------
    // Test 3: projectArea returns a cells array with the expected structure
    // -----------------------------------------------------------------------

    [Fact]
    public void ProjectArea_ReturnsCells_WithExpectedCountAndShape()
    {
        var (rt, world) = BuildRuntime();
        AddCastleBox(world);

        // Store JS results in individual Evaluate calls so we can assert in C#.
        // The projector deterministically roots at the lexicographically lowest id,
        // which for "castle:castle-0..3" is "castle:castle-0".
        var cellCount = EsmTest.Eval(rt, """
            tapestry.world.projectArea('castle:castle-0', { scope: 'area' }).cells.length;
            """);

        var firstId = EsmTest.Eval(rt, """
            tapestry.world.projectArea('castle:castle-0', { scope: 'area' }).cells[0].id;
            """);

        var firstXType = EsmTest.Eval(rt, """
            typeof tapestry.world.projectArea('castle:castle-0', { scope: 'area' }).cells[0].x;
            """);

        Convert.ToInt32(cellCount).Should().Be(4,
            "the 4-room box area should project to exactly 4 cells");

        firstId?.ToString().Should().Be("castle:castle-0",
            "cells are ordered by id (StringComparer.Ordinal) so castle-0 is first");

        firstXType?.ToString().Should().Be("number",
            "the x coordinate of every cell should be a JS number");
    }

    // -----------------------------------------------------------------------
    // Test 4: renderAreaMap clamps a NaN radius to the default 3-hop radius
    // -----------------------------------------------------------------------

    [Fact]
    public void RenderAreaMap_clamps_degenerate_radius_to_default()
    {
        // NaN radius must not produce a degenerate single-room map — it falls back to
        // the default 3-hop radius, so the neighboring room is still visible.
        var (rt, world) = BuildRuntime();
        AddCastleBox(world);

        var result = EsmTest.Eval(rt, """
            tapestry.world.renderAreaMap('castle:castle-0', {
                scope: 'radius',
                radius: NaN,
                label: 'name',
                showCurrent: true
            });
            """);

        var text = result?.ToString() ?? "";
        text.Should().Contain("East Tower",
            "with a clamped 3-hop radius the east neighbour (castle-1) is reachable and its name appears in the legend");
    }

    // -----------------------------------------------------------------------
    // Plane fix: whole-area renders follow the viewer's z-plane
    // -----------------------------------------------------------------------

    [Fact]
    public void RenderAreaMap_WholeArea_FollowsViewersPlane()
    {
        var (rt, world) = BuildRuntime();
        AddCastleBox(world);
        AddTowerAbove(world);

        // The whole-area projection roots at the lexicographically lowest id
        // (castle:castle-0, plane 0); the viewer stands on the tower (plane 1).
        var result = EsmTest.Eval(rt, """
            tapestry.world.renderAreaMap('castle:tower-top', {
                scope: 'area',
                label: 'name',
                showCurrent: true
            });
            """);

        var text = result?.ToString() ?? "";
        text.Should().Contain("Tower Top",
            "the rendered plane must be the one the viewer is standing on");
        text.Should().NotContain("There is nothing to map on this level.");
    }

    [Fact]
    public void RenderAreaMap_WholeArea_GroundFloorViewer_StillRendersPlaneZero()
    {
        var (rt, world) = BuildRuntime();
        AddCastleBox(world);
        AddTowerAbove(world);

        var result = EsmTest.Eval(rt, """
            tapestry.world.renderAreaMap('castle:castle-0', {
                scope: 'area',
                label: 'name',
                showCurrent: true
            });
            """);

        var text = result?.ToString() ?? "";
        text.Should().Contain("Castle Gate",
            "a ground-floor viewer still gets plane 0 (regression guard)");
    }
}
