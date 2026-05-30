using System.Collections.Generic;
using System.IO;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Scripting.Modules;
using Xunit;

namespace Tapestry.Scripting.Tests;

public class WorldAuthoringModuleTests
{
    private static (WorldAuthoringModule mod, World world, string root) Build()
    {
        var root = Path.Combine(Path.GetTempPath(), "authmod-" + Path.GetRandomFileName());
        var world = new World();
        var props = new PropertyRegistry();
        props.RegisterEngineProperty("terrain", "t", PropertyValueType.String, appliesTo: new[] { EntityTypes.Room });
        var tags = new TagRegistry();
        var projector = new RoomProjector(world, props, tags);
        var writer = new AttributeWriter(props, tags);
        var loadedPackNamespaces = new HashSet<string> { "legends-forgotten" };
        var mod = new WorldAuthoringModule(world, projector, writer, root, loadedPackNamespaces);
        return (mod, world, root);
    }

    [Fact]
    public void CreateRoom_adds_room_and_writes_sidecar()
    {
        var (mod, world, root) = Build();
        var ok = mod.CreateRoom("lf-test", "legends-forgotten:anchor", "Anchor", "An anchor room.");
        Assert.True(ok);
        Assert.NotNull(world.GetRoom("legends-forgotten:anchor"));
        var file = Path.Combine(root, "lf-test", "rooms", "anchor.yaml");
        Assert.True(File.Exists(file));
        var text = File.ReadAllText(file);
        Assert.Contains("legends-forgotten:anchor", text);
        Assert.DoesNotContain("neighbors", text);  // recommend-context must not leak
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void CreateRoom_rejects_duplicate_id()
    {
        var (mod, _, root) = Build();
        Assert.True(mod.CreateRoom("lf-test", "legends-forgotten:anchor", "A", "d"));
        Assert.False(mod.CreateRoom("lf-test", "legends-forgotten:anchor", "A", "d"));
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void CreateRoom_rejects_namespace_not_loaded()
    {
        var (mod, _, root) = Build();
        Assert.False(mod.CreateRoom("x", "unknownpack:room", "A", "d"));
        if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void SetRoomExit_then_sidecar_roundtrips_through_LoadRoom()
    {
        var (mod, world, root) = Build();
        mod.CreateRoom("lf-test", "legends-forgotten:a", "A", "da");
        mod.CreateRoom("lf-test", "legends-forgotten:b", "B", "db");
        Assert.True(mod.SetRoomExit("legends-forgotten:a", "north", "legends-forgotten:b"));
        var yaml = File.ReadAllText(Path.Combine(root, "lf-test", "rooms", "a.yaml"));
        Assert.DoesNotContain("neighbors", yaml);  // suppression guard: room 'a' HAS a neighbor, so this has teeth
        var reloaded = Tapestry.Scripting.YamlContentLoader.LoadRoom(yaml).Room;
        Assert.Equal("legends-forgotten:b", reloaded.GetExit(Tapestry.Shared.Direction.North)!.TargetRoomId);
        Directory.Delete(root, recursive: true);
    }
}
