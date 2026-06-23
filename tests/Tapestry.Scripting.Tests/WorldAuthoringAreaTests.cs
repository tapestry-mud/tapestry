using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Scripting.Connections;
using Tapestry.Scripting.Modules;
using Xunit;

namespace Tapestry.Scripting.Tests;

/// <summary>Harness that wires a real WorldAuthoringModule for area authoring tests.</summary>
internal sealed class AreaAuthoringHarness
{
    public WorldAuthoringModule Module { get; }
    public AreaRegistry Registry { get; }
    public string Root { get; }

    private AreaAuthoringHarness(WorldAuthoringModule module, AreaRegistry registry, string root)
    {
        Module = module;
        Registry = registry;
        Root = root;
    }

    public static AreaAuthoringHarness New()
    {
        var root = Path.Combine(Path.GetTempPath(), "tap-areaauth-" + Path.GetRandomFileName());
        var world = new World();
        var props = new PropertyRegistry();
        var tags = new TagRegistry();
        var projector = new RoomProjector(world, props, tags, new AreaRegistry());
        var writer = new AttributeWriter(props, tags);
        var loadedPackNamespaces = new HashSet<string> { "legends-forgotten" };
        var connections = new ConnectionLoader(
            world, NullLogger<ConnectionLoader>.Instance, Path.Combine(root, "connections"));
        var registry = new AreaRegistry();
        var mod = new WorldAuthoringModule(
            world, projector, writer, root, loadedPackNamespaces,
            registry, new StubExitResolver(), new OracleTableRegistry(), recommend: null, connections: connections);
        return new AreaAuthoringHarness(mod, registry, root);
    }
}

public class WorldAuthoringAreaTests
{
    [Fact]
    public void CreateArea_WritesSideCar_AndRegisters()
    {
        var h = AreaAuthoringHarness.New();
        var ok = h.Module.CreateArea("road-to-tar-valon", "Road to Tar Valon");

        Assert.True(ok);
        Assert.True(h.Registry.Contains("road-to-tar-valon"));
        Assert.True(File.Exists(Path.Combine(h.Root, "road-to-tar-valon", "area.yaml")));
        Assert.Null(h.Registry.Get("road-to-tar-valon")!.SourcePack); // authored

        Directory.Delete(h.Root, recursive: true);
    }

    [Fact]
    public void CreateArea_OnDuplicate_ReturnsFalse_AndDoesNotOverwrite()
    {
        var h = AreaAuthoringHarness.New();
        h.Registry.Register(new AreaDefinition { Id = "road-to-tar-valon", Name = "Existing", Theme = "keep" });

        var ok = h.Module.CreateArea("road-to-tar-valon", "Road to Tar Valon");

        Assert.False(ok);
        Assert.Equal("keep", h.Registry.Get("road-to-tar-valon")!.Theme);

        if (Directory.Exists(h.Root)) { Directory.Delete(h.Root, recursive: true); }
    }

    [Fact]
    public void GetArea_ReturnsFieldsAndProvenanceInputs()
    {
        var h = AreaAuthoringHarness.New();
        h.Module.CreateArea("road-to-tar-valon", "Road");

        var info = h.Module.GetArea("road-to-tar-valon");
        Assert.Equal("road-to-tar-valon", info.Id);
        Assert.Equal("Road", info.Name);
        Assert.Null(info.SourcePack);
        Assert.True(info.SideCar);
        Assert.True(info.Exists);

        Directory.Delete(h.Root, recursive: true);
    }

    [Fact]
    public void SetAreaTheme_UpdatesRegistry_AndRewritesSideCar()
    {
        var h = AreaAuthoringHarness.New();
        h.Module.CreateArea("road-to-tar-valon", "Road");

        var ok = h.Module.SetAreaTheme("road-to-tar-valon", "Grim pilgrimage under a watchful Tower.");

        Assert.True(ok);
        Assert.Equal("Grim pilgrimage under a watchful Tower.", h.Registry.Get("road-to-tar-valon")!.Theme);
        var yaml = System.IO.File.ReadAllText(System.IO.Path.Combine(h.Root, "road-to-tar-valon", "area.yaml"));
        Assert.Contains("Grim pilgrimage", yaml);
    }

    [Fact]
    public void SetAreaX_OnMissingArea_ReturnsFalse()
    {
        var h = AreaAuthoringHarness.New();
        Assert.False(h.Module.SetAreaTheme("nope", "x"));
    }

    [Fact]
    public void SetAreaAttribute_LevelRange_ParsesPair()
    {
        var h = AreaAuthoringHarness.New();
        h.Module.CreateArea("road-to-tar-valon", "Road");

        var msg = h.Module.SetAreaAttribute("road-to-tar-valon", "level_range", "5,12");

        Assert.Equal(new[] { 5, 12 }, h.Registry.Get("road-to-tar-valon")!.LevelRange);
        Assert.Contains("level_range", msg);
    }

    [Fact]
    public void SetAreaAttribute_ResetInterval_ParsesInt()
    {
        var h = AreaAuthoringHarness.New();
        h.Module.CreateArea("road-to-tar-valon", "Road");

        h.Module.SetAreaAttribute("road-to-tar-valon", "reset_interval", "300");

        Assert.Equal(300, h.Registry.Get("road-to-tar-valon")!.ResetInterval);
    }

    [Fact]
    public void GetAreas_ReturnsSummaries_WithProvenanceTags_SortedByLevel()
    {
        var h = AreaAuthoringHarness.New();
        // Authored area (side-car written by CreateArea, no source_pack)
        h.Module.CreateArea("low", "Low");
        h.Module.SetAreaAttribute("low", "level_range", "1,5");
        // Packed area registered directly (source_pack, no side-car)
        h.Registry.Register(new AreaDefinition { Id = "high", Name = "High", LevelRange = new[] { 20, 30 }, SourcePack = "@mallek/lf" });

        var list = h.Module.GetAreas();

        Assert.Equal(2, list.Count);
        Assert.Equal("low", list[0].Id);                  // sorted by level range low->high
        Assert.Equal("[authored]", list[0].Provenance);
        Assert.Equal("high", list[1].Id);
        Assert.Equal("[pack]", list[1].Provenance);

        Directory.Delete(h.Root, recursive: true);
    }

    [Fact]
    public void GetAreaRooms_TagsEachRoomProvenance()
    {
        var h = AreaAuthoringHarness.New();
        h.Module.CreateArea("road-to-tar-valon", "Road");
        // Authored room: CreateRoom writes a side-car, no source_pack.
        // roomId must be namespaced (ns:key) and the ns must be in loadedPackNamespaces.
        h.Module.CreateRoom("road-to-tar-valon", "legends-forgotten:road-to-tar-valon-1", "Track", "A dusty track.");

        var rooms = h.Module.GetAreaRooms("road-to-tar-valon");

        Assert.Single(rooms);
        Assert.Equal("legends-forgotten:road-to-tar-valon-1", rooms[0].Id);
        Assert.Equal("Track", rooms[0].Name);
        Assert.Equal("[authored]", rooms[0].Provenance);

        Directory.Delete(h.Root, recursive: true);
    }

    [Fact]
    public void SetAreaAttribute_Wip_AddsFlag_AndPersists()
    {
        var h = AreaAuthoringHarness.New();
        h.Module.CreateArea("road-to-tar-valon", "Road");

        var msg = h.Module.SetAreaAttribute("road-to-tar-valon", "wip", "true");

        Assert.Contains("wip", msg);
        Assert.Contains("wip", h.Registry.Get("road-to-tar-valon")!.Flags);
        var yaml = System.IO.File.ReadAllText(
            System.IO.Path.Combine(h.Root, "road-to-tar-valon", "area.yaml"));
        Assert.Contains("wip", yaml);
    }

    [Fact]
    public void SetAreaAttribute_WipFalse_RemovesFlag()
    {
        var h = AreaAuthoringHarness.New();
        h.Module.CreateArea("road-to-tar-valon", "Road");
        h.Module.SetAreaAttribute("road-to-tar-valon", "wip", "true");

        h.Module.SetAreaAttribute("road-to-tar-valon", "wip", "false");

        Assert.DoesNotContain("wip", h.Registry.Get("road-to-tar-valon")!.Flags);
    }

    [Fact]
    public void GetArea_ExposesWipState()
    {
        var h = AreaAuthoringHarness.New();
        h.Module.CreateArea("road-to-tar-valon", "Road");
        Assert.False(h.Module.GetArea("road-to-tar-valon").Wip);

        h.Module.SetAreaAttribute("road-to-tar-valon", "wip", "true");
        Assert.True(h.Module.GetArea("road-to-tar-valon").Wip);
    }

    [Fact]
    public void GetAreas_ExcludesWip_WhenIncludeWipFalse_ButTagsItWhenIncluded()
    {
        var h = AreaAuthoringHarness.New();
        h.Module.CreateArea("open-zone", "Open");
        h.Module.CreateArea("half-dug", "Half");
        h.Module.SetAreaAttribute("half-dug", "wip", "true");

        var playerView = h.Module.GetAreas(includeWip: false);
        Assert.DoesNotContain(playerView, a => a.Id == "half-dug");

        var builderView = h.Module.GetAreas(includeWip: true);
        var wipRow = Assert.Single(builderView, a => a.Id == "half-dug");
        Assert.True(wipRow.Wip);

        Directory.Delete(h.Root, recursive: true);
    }
}
