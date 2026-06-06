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
            registry, recommend: null, connections: connections);
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
}
