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
        var projector = new RoomProjector(world, props, tags, new AreaRegistry());
        var writer = new AttributeWriter(props, tags);
        var loadedPackNamespaces = new HashSet<string> { "legends-forgotten" };
        var connections = new ConnectionLoader(
            world, NullLogger<ConnectionLoader>.Instance, Path.Combine(root, "connections"));
        var mod = new WorldAuthoringModule(
            world, projector, writer, root, loadedPackNamespaces,
            new AreaRegistry(), new StubExitResolver(), new OracleTableRegistry(), recommend: null, connections: connections);
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

    // ----- SetRoomName: re-key + side-car persistence -----

    [Fact]
    public void SetRoomName_rekeys_id_and_renames_sidecar()
    {
        var (mod, world, root, _) = Build();
        mod.CreateRoom("lf-test", "legends-forgotten:lf-test-1", "New Room", "A bare room.");

        var result = mod.SetRoomName("legends-forgotten:lf-test-1", "The Gatehouse");

        Assert.True(result.Ok);
        Assert.True(result.Renamed);
        Assert.Equal("legends-forgotten:gatehouse", result.Id);
        Assert.Empty(result.Warnings);

        // Live world re-keyed:
        Assert.Null(world.GetRoom("legends-forgotten:lf-test-1"));
        Assert.NotNull(world.GetRoom("legends-forgotten:gatehouse"));

        // Side-car renamed on disk:
        Assert.False(File.Exists(Path.Combine(root, "lf-test", "rooms", "lf-test-1.yaml")));
        var newFile = Path.Combine(root, "lf-test", "rooms", "gatehouse.yaml");
        Assert.True(File.Exists(newFile));
        Assert.Contains("legends-forgotten:gatehouse", File.ReadAllText(newFile));

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void SetRoomName_rewrites_neighbor_sidecars()
    {
        var (mod, world, root, _) = Build();
        mod.CreateRoom("lf-test", "legends-forgotten:lf-test-1", "New Room", "d");
        mod.CreateRoom("lf-test", "legends-forgotten:lf-test-2", "Neighbor", "d");
        mod.SetRoomExit("legends-forgotten:lf-test-2", "north", "legends-forgotten:lf-test-1");
        mod.SetRoomExit("legends-forgotten:lf-test-1", "south", "legends-forgotten:lf-test-2");

        var result = mod.SetRoomName("legends-forgotten:lf-test-1", "The Gatehouse");

        Assert.True(result.Renamed);

        // Neighbor's live exit retargeted (RekeyRoom):
        var neighbor = world.GetRoom("legends-forgotten:lf-test-2")!;
        Assert.Equal("legends-forgotten:gatehouse",
            neighbor.GetExit(Direction.North)!.TargetRoomId);

        // Neighbor's side-car rewritten on disk — round-trip through the real loader:
        var neighborYaml = File.ReadAllText(Path.Combine(root, "lf-test", "rooms", "lf-test-2.yaml"));
        var reloaded = YamlContentLoader.LoadRoom(neighborYaml).Room;
        Assert.Equal("legends-forgotten:gatehouse",
            reloaded.GetExit(Direction.North)!.TargetRoomId);

        // The renamed room's own side-car keeps its outgoing exit:
        var ownYaml = File.ReadAllText(Path.Combine(root, "lf-test", "rooms", "gatehouse.yaml"));
        var reloadedOwn = YamlContentLoader.LoadRoom(ownYaml).Room;
        Assert.Equal("legends-forgotten:lf-test-2",
            reloadedOwn.GetExit(Direction.South)!.TargetRoomId);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void SetRoomName_collision_appends_suffix()
    {
        var (mod, world, root, _) = Build();
        mod.CreateRoom("lf-test", "legends-forgotten:gatehouse", "The Gatehouse", "d");
        mod.CreateRoom("lf-test", "legends-forgotten:lf-test-2", "New Room", "d");

        var result = mod.SetRoomName("legends-forgotten:lf-test-2", "The Gatehouse");

        Assert.True(result.Renamed);
        Assert.Equal("legends-forgotten:gatehouse-2", result.Id);
        Assert.NotNull(world.GetRoom("legends-forgotten:gatehouse-2"));
        Assert.True(File.Exists(Path.Combine(root, "lf-test", "rooms", "gatehouse-2.yaml")));

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void SetRoomName_resaving_same_name_on_suffixed_room_is_noop()
    {
        // gatehouse-2's own key must not count as "taken" — otherwise every re-save of
        // the same name walks the suffix up (-3, -4, ...).
        var (mod, world, root, _) = Build();
        mod.CreateRoom("lf-test", "legends-forgotten:gatehouse", "The Gatehouse", "d");
        mod.CreateRoom("lf-test", "legends-forgotten:lf-test-2", "New Room", "d");
        mod.SetRoomName("legends-forgotten:lf-test-2", "The Gatehouse"); // -> gatehouse-2

        var result = mod.SetRoomName("legends-forgotten:gatehouse-2", "The Gatehouse");

        Assert.True(result.Ok);
        Assert.False(result.Renamed);
        Assert.Equal("legends-forgotten:gatehouse-2", result.Id);
        Assert.NotNull(world.GetRoom("legends-forgotten:gatehouse-2"));
        Assert.Null(world.GetRoom("legends-forgotten:gatehouse-3"));

        Directory.Delete(root, recursive: true);
    }

    // ----- SetRoomName: edge triage (connections fixed, hardcoded edges warned) -----

    [Fact]
    public void SetRoomName_pack_room_referencer_warns_and_is_untouched()
    {
        var (mod, world, root, _) = Build();
        mod.CreateRoom("lf-test", "legends-forgotten:lf-test-1", "New Room", "d");
        // A pack room with a hardcoded exit into the authored room:
        var packRoom = new Room("legends-forgotten:cellar", "Cellar", "d") { Area = "lf-pack-area" };
        packRoom.SetProperty("source_pack", "legends-forgotten");
        packRoom.SetExit(Direction.Up, new Exit("legends-forgotten:lf-test-1"));
        world.AddRoom(packRoom);

        var result = mod.SetRoomName("legends-forgotten:lf-test-1", "The Gatehouse");

        Assert.True(result.Renamed);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("legends-forgotten:cellar", warning);
        Assert.Contains("pack room", warning);
        // The pack room's exit is untouched (dangles visibly, by design):
        Assert.Equal("legends-forgotten:lf-test-1", packRoom.GetExit(Direction.Up)!.TargetRoomId);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void SetRoomName_other_area_referencer_warns()
    {
        var (mod, world, root, _) = Build();
        mod.CreateRoom("lf-test", "legends-forgotten:lf-test-1", "New Room", "d");
        mod.CreateRoom("other-area", "legends-forgotten:far-room", "Far Room", "d");
        mod.SetRoomExit("legends-forgotten:far-room", "west", "legends-forgotten:lf-test-1");

        var result = mod.SetRoomName("legends-forgotten:lf-test-1", "The Gatehouse");

        Assert.True(result.Renamed);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("legends-forgotten:far-room", warning);
        // Other-area exit untouched in memory and on disk:
        Assert.Equal("legends-forgotten:lf-test-1",
            world.GetRoom("legends-forgotten:far-room")!.GetExit(Direction.West)!.TargetRoomId);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void SetRoomName_link_backed_referencer_fixed_with_no_warning()
    {
        var (mod, world, root, connections) = Build();
        mod.CreateRoom("lf-test", "legends-forgotten:lf-test-1", "New Room", "d");
        mod.CreateRoom("other-area", "legends-forgotten:plaza", "Plaza", "d");

        // A link (connection) between the two: plaza --north--> lf-test-1.
        var plaza = world.GetRoom("legends-forgotten:plaza")!;
        plaza.SetExit(Direction.North, new Exit("legends-forgotten:lf-test-1"));
        var record = new ConnectionRecord
        {
            Id = "legends-forgotten_plaza--north--legends-forgotten_lf-test-1",
            From = new ConnectionSide { Room = "legends-forgotten:plaza", Type = "direction", Direction = "north" },
            To = new ConnectionSide { Room = "legends-forgotten:lf-test-1", Type = "direction", Direction = "south" },
            CreatedBy = "test",
            CreatedAt = DateTimeOffset.UtcNow
        };
        connections.AddLoaded(record);
        // The Build() fixture constructs the loader with this path — compute it the same
        // way rather than relying on a ConnectionsDirectory property.
        var connDir = Path.Combine(root, "connections");
        Directory.CreateDirectory(connDir);
        File.WriteAllText(Path.Combine(connDir, $"{record.Id}.yaml"), "placeholder");

        var result = mod.SetRoomName("legends-forgotten:lf-test-1", "The Gatehouse");

        Assert.True(result.Renamed);
        Assert.Empty(result.Warnings);

        // Connection record updated + persisted:
        Assert.Equal("legends-forgotten:gatehouse", record.To.Room);
        var recordYaml = File.ReadAllText(Path.Combine(connDir, $"{record.Id}.yaml"));
        Assert.Contains("legends-forgotten:gatehouse", recordYaml);

        // The link-backed room's in-memory exit retargeted:
        Assert.Equal("legends-forgotten:gatehouse",
            plaza.GetExit(Direction.North)!.TargetRoomId);

        Directory.Delete(root, recursive: true);
    }

    // ----- Side-car writes never persist connection-backed exits (composition leak guard) -----

    [Fact]
    public void SetRoomName_does_not_bake_connection_backed_exit_into_sidecar()
    {
        var (mod, world, root, connections) = Build();
        mod.CreateRoom("lf-test", "legends-forgotten:lf-test-1", "New Room", "d");
        mod.CreateRoom("lf-test", "legends-forgotten:lf-test-2", "Neighbor", "d");
        // An authored exit (should persist):
        mod.SetRoomExit("legends-forgotten:lf-test-1", "north", "legends-forgotten:lf-test-2");

        // A connection-backed exit (should NOT persist): link lf-test-1 south to a pack room.
        var packRoom = new Room("tapestry-core:recall", "Recall", "d") { Area = "core-area" };
        packRoom.SetProperty("source_pack", "tapestry-core");
        world.AddRoom(packRoom);
        var room = world.GetRoom("legends-forgotten:lf-test-1")!;
        room.SetExit(Direction.South, new Exit("tapestry-core:recall"));
        connections.AddLoaded(new ConnectionRecord
        {
            Id = "lf-test-1--south--recall",
            From = new ConnectionSide { Room = "legends-forgotten:lf-test-1", Type = "direction", Direction = "South" },
            To = new ConnectionSide { Room = "tapestry-core:recall", Type = "direction", Direction = "North" },
            CreatedBy = "test",
            CreatedAt = DateTimeOffset.UtcNow
        });
        Directory.CreateDirectory(Path.Combine(root, "connections"));

        // Rename triggers WriteSideCar on the renamed room.
        var result = mod.SetRoomName("legends-forgotten:lf-test-1", "The Gatehouse");

        Assert.True(result.Renamed);
        var yaml = File.ReadAllText(Path.Combine(root, "lf-test", "rooms", "gatehouse.yaml"));
        // The authored north exit persists; the connection-backed south exit does not.
        Assert.Contains("legends-forgotten:lf-test-2", yaml);
        Assert.DoesNotContain("tapestry-core:recall", yaml);

        // The in-memory exit is untouched (still walkable live):
        Assert.Equal("tapestry-core:recall",
            world.GetRoom("legends-forgotten:gatehouse")!.GetExit(Direction.South)!.TargetRoomId);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void SetRoomDescription_also_does_not_bake_connection_backed_exit()
    {
        // The leak guard applies to every authoring write, not just rename.
        var (mod, world, root, connections) = Build();
        mod.CreateRoom("lf-test", "legends-forgotten:lf-test-1", "New Room", "d");
        var room = world.GetRoom("legends-forgotten:lf-test-1")!;
        room.SetExit(Direction.South, new Exit("tapestry-core:recall"));
        connections.AddLoaded(new ConnectionRecord
        {
            Id = "lf-test-1--south--recall",
            From = new ConnectionSide { Room = "legends-forgotten:lf-test-1", Type = "direction", Direction = "south" },
            To = new ConnectionSide { Room = "tapestry-core:recall", Type = "direction", Direction = "north" },
            CreatedBy = "test",
            CreatedAt = DateTimeOffset.UtcNow
        });

        mod.SetRoomDescription("legends-forgotten:lf-test-1", "A described room.");

        var yaml = File.ReadAllText(Path.Combine(root, "lf-test", "rooms", "lf-test-1.yaml"));
        Assert.DoesNotContain("tapestry-core:recall", yaml);
        Assert.Contains("A described room.", yaml);

        Directory.Delete(root, recursive: true);
    }
}
