using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Scripting.Connections;
using Tapestry.Scripting.Modules;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Scripting.Tests;

public class WorldAuthoringModuleTests
{
    private static (WorldAuthoringModule mod, World world, string root, ConnectionLoader connections) Build()
    {
        var root = Path.Combine(Path.GetTempPath(), "authmod-" + Path.GetRandomFileName());
        var world = new World();
        var props = new PropertyRegistry();
        props.RegisterEngineProperty("terrain", "t", PropertyValueType.String, appliesTo: new[] { EntityTypes.Room });
        var tags = new TagRegistry();
        var projector = new RoomProjector(world, props, tags);
        var writer = new AttributeWriter(props, tags);
        var loadedPackNamespaces = new HashSet<string> { "legends-forgotten" };
        var connections = new ConnectionLoader(
            world, NullLogger<ConnectionLoader>.Instance, Path.Combine(root, "connections"));
        var mod = new WorldAuthoringModule(
            world, projector, writer, root, loadedPackNamespaces,
            recommend: null, connections: connections);
        return (mod, world, root, connections);
    }

    [Fact]
    public void CreateRoom_adds_room_and_writes_sidecar()
    {
        var (mod, world, root, _) = Build();
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
        var (mod, _, root, _) = Build();
        Assert.True(mod.CreateRoom("lf-test", "legends-forgotten:anchor", "A", "d"));
        Assert.False(mod.CreateRoom("lf-test", "legends-forgotten:anchor", "A", "d"));
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void CreateRoom_rejects_namespace_not_loaded()
    {
        var (mod, _, root, _) = Build();
        Assert.False(mod.CreateRoom("x", "unknownpack:room", "A", "d"));
        if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void SetRoomExit_then_sidecar_roundtrips_through_LoadRoom()
    {
        var (mod, world, root, _) = Build();
        mod.CreateRoom("lf-test", "legends-forgotten:a", "A", "da");
        mod.CreateRoom("lf-test", "legends-forgotten:b", "B", "db");
        Assert.True(mod.SetRoomExit("legends-forgotten:a", "north", "legends-forgotten:b"));
        var yaml = File.ReadAllText(Path.Combine(root, "lf-test", "rooms", "a.yaml"));
        Assert.DoesNotContain("neighbors", yaml);  // suppression guard: room 'a' HAS a neighbor, so this has teeth
        var reloaded = Tapestry.Scripting.YamlContentLoader.LoadRoom(yaml).Room;
        Assert.Equal("legends-forgotten:b", reloaded.GetExit(Tapestry.Shared.Direction.North)!.TargetRoomId);
        Directory.Delete(root, recursive: true);
    }

    // ----- SetRoomName: name-only paths (no rekey) -----

    [Fact]
    public void SetRoomName_missing_room_returns_not_ok()
    {
        var (mod, _, root, _) = Build();

        var result = mod.SetRoomName("legends-forgotten:nope", "Anything");

        Assert.False(result.Ok);
        Assert.False(result.Renamed);
        if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void SetRoomName_pack_room_changes_name_only_never_rekeys()
    {
        var (mod, world, root, _) = Build();
        // A pack room: source_pack property set (the loader does this for every pack room).
        var packRoom = new Room("legends-forgotten:cellar", "Old Name", "d") { Area = "lf-area" };
        packRoom.SetProperty("source_pack", "legends-forgotten");
        world.AddRoom(packRoom);

        var result = mod.SetRoomName("legends-forgotten:cellar", "The Gatehouse");

        Assert.True(result.Ok);
        Assert.False(result.Renamed);
        Assert.Equal("legends-forgotten:cellar", result.Id);
        Assert.Equal("The Gatehouse", packRoom.Name);
        Assert.NotNull(world.GetRoom("legends-forgotten:cellar"));
        Assert.Null(world.GetRoom("legends-forgotten:gatehouse"));
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void SetRoomName_unusable_slug_keeps_current_id()
    {
        var (mod, world, root, _) = Build();
        mod.CreateRoom("lf-test", "legends-forgotten:lf-test-1", "New Room", "d");

        var result = mod.SetRoomName("legends-forgotten:lf-test-1", "!!!");

        Assert.True(result.Ok);
        Assert.False(result.Renamed);
        Assert.Equal("legends-forgotten:lf-test-1", result.Id);
        Assert.Equal("!!!", world.GetRoom("legends-forgotten:lf-test-1")!.Name);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void SetRoomName_same_slug_is_noop_rekey()
    {
        var (mod, world, root, _) = Build();
        mod.CreateRoom("lf-test", "legends-forgotten:gatehouse", "Old", "d");

        var result = mod.SetRoomName("legends-forgotten:gatehouse", "The Gatehouse");

        Assert.True(result.Ok);
        Assert.False(result.Renamed);
        Assert.Equal("legends-forgotten:gatehouse", result.Id);
        Assert.Equal("The Gatehouse", world.GetRoom("legends-forgotten:gatehouse")!.Name);
        Directory.Delete(root, recursive: true);
    }
}
