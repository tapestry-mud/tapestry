using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Consequence;
using Tapestry.Engine.Items;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Scripting.Connections;
using Tapestry.Scripting.Modules;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Scripting.Tests;

/// <summary>Wires a WorldAuthoringModule with the full teardown dependency set:
/// item registry, oracle registry, consequence overlay, event bus.</summary>
internal sealed class AreaTeardownHarness
{
    public WorldAuthoringModule Module { get; init; } = null!;
    public World World { get; init; } = null!;
    public AreaRegistry Areas { get; init; } = null!;
    public OracleTableRegistry Oracles { get; init; } = null!;
    public ItemRegistry Items { get; init; } = null!;
    public ConsequenceOverlay Consequences { get; init; } = null!;
    public string Root { get; init; } = "";

    public static AreaTeardownHarness New()
    {
        var root = Path.Combine(Path.GetTempPath(), "tap-teardown-" + Path.GetRandomFileName());
        var world = new World();
        var props = new PropertyRegistry();
        var tags = new TagRegistry();
        var areas = new AreaRegistry();
        var oracles = new OracleTableRegistry();
        var items = new ItemRegistry();
        var eventBus = new EventBus();
        var consequences = new ConsequenceOverlay(world, eventBus);
        var projector = new RoomProjector(world, props, tags, new AreaRegistry());
        var writer = new AttributeWriter(props, tags);
        var connections = new ConnectionLoader(
            world, NullLogger<ConnectionLoader>.Instance, Path.Combine(root, "connections"));

        var module = new WorldAuthoringModule(
            world, projector, writer, root, new HashSet<string> { "oracle-run" },
            areas, new StubExitResolver(), oracles,
            recommend: null, connections: connections, gameLoop: null, logger: null,
            metrics: null, packsRoot: null, itemRegistry: items, runtimeNamespaces: null,
            consequences: consequences, eventBus: eventBus);

        return new AreaTeardownHarness
        {
            Module = module, World = world, Areas = areas, Oracles = oracles,
            Items = items, Consequences = consequences, Root = root
        };
    }

    /// <summary>Adds a room to the World and registers it under the given area.</summary>
    public Room AddRoom(string id, string areaId)
    {
        var room = new Room(id, id, "") { Area = areaId };
        World.AddRoom(room);
        return room;
    }

    public Entity AddPlayer(string roomId, string name)
    {
        var player = new Entity(EntityTypes.Player, name);
        World.GetRoom(roomId)!.AddEntity(player);
        return player;
    }

    public void Cleanup()
    {
        if (Directory.Exists(Root)) { Directory.Delete(Root, recursive: true); }
    }
}

public class WorldAuthoringEvacuateTests
{
    [Fact]
    public void EvacuateArea_MovesEveryPlayerToRecall_AndLeavesMobsAlone()
    {
        var h = AreaTeardownHarness.New();
        h.AddRoom("tapestry-core:recall", "tapestry-core");
        h.AddRoom("oracle-run:oracle-run-3f2a-entry", "oracle-run-3f2a");
        h.AddRoom("oracle-run:oracle-run-3f2a-1_0_0", "oracle-run-3f2a");
        var p1 = h.AddPlayer("oracle-run:oracle-run-3f2a-entry", "Alice");
        var p2 = h.AddPlayer("oracle-run:oracle-run-3f2a-1_0_0", "Bob");
        var mob = new Entity(EntityTypes.Npc, "a rat");
        h.World.GetRoom("oracle-run:oracle-run-3f2a-entry")!.AddEntity(mob);

        var moved = h.Module.EvacuateArea("oracle-run-3f2a", "tapestry-core:recall");

        Assert.Equal(2, moved);
        Assert.Equal("tapestry-core:recall", p1.LocationRoomId);
        Assert.Equal("tapestry-core:recall", p2.LocationRoomId);
        Assert.Equal("oracle-run:oracle-run-3f2a-entry", mob.LocationRoomId);
        h.Cleanup();
    }

    [Fact]
    public void EvacuateArea_WithNoPlayers_ReturnsZero_EvenWhenRecallRoomMissing()
    {
        var h = AreaTeardownHarness.New();
        h.AddRoom("oracle-run:oracle-run-3f2a-entry", "oracle-run-3f2a");

        Assert.Equal(0, h.Module.EvacuateArea("oracle-run-3f2a", "tapestry-core:recall"));
        h.Cleanup();
    }

    [Fact]
    public void EvacuateArea_WithPlayers_ButNoRecallRoom_ReturnsMinusOne_AndMovesNobody()
    {
        var h = AreaTeardownHarness.New();
        h.AddRoom("oracle-run:oracle-run-3f2a-entry", "oracle-run-3f2a");
        var p1 = h.AddPlayer("oracle-run:oracle-run-3f2a-entry", "Alice");

        Assert.Equal(-1, h.Module.EvacuateArea("oracle-run-3f2a", "tapestry-core:recall"));
        Assert.Equal("oracle-run:oracle-run-3f2a-entry", p1.LocationRoomId);
        h.Cleanup();
    }

    [Fact]
    public void EvacuateArea_NullRecallRoomId_UsesTheDefault()
    {
        var h = AreaTeardownHarness.New();
        h.AddRoom(WorldAuthoringModule.DefaultRecallRoomId, "tapestry-core");
        h.AddRoom("oracle-run:oracle-run-3f2a-entry", "oracle-run-3f2a");
        var p1 = h.AddPlayer("oracle-run:oracle-run-3f2a-entry", "Alice");

        Assert.Equal(1, h.Module.EvacuateArea("oracle-run-3f2a", null));
        Assert.Equal(WorldAuthoringModule.DefaultRecallRoomId, p1.LocationRoomId);
        h.Cleanup();
    }
}

public class WorldAuthoringDeleteAreaTests
{
    [Fact]
    public void DeleteArea_SweepsRooms_Entities_Registries_AndDirectory()
    {
        var h = AreaTeardownHarness.New();
        h.AddRoom("tapestry-core:recall", "tapestry-core");
        h.Module.CreateArea("oracle-run-3f2a", "the Ashen Hollow");
        h.AddRoom("oracle-run:oracle-run-3f2a-entry", "oracle-run-3f2a");
        h.AddRoom("oracle-run:oracle-run-3f2a-1_0_0", "oracle-run-3f2a");

        var mob = new Entity(EntityTypes.Npc, "a rat");
        h.World.GetRoom("oracle-run:oracle-run-3f2a-entry")!.AddEntity(mob);
        h.World.TrackEntity(mob);

        h.Oracles.Register(new OracleTable { Id = "oracle-run-3f2a:mobs", Kind = "mobs" });
        h.Items.Register(new ItemTemplate { Id = "oracle-run-3f2a:kit-p1-wield", Name = "a blade" });
        h.Consequences.Stamp("oracle-run:oracle-run-3f2a-entry", "looted", "ephemeral");

        var areaDir = Path.Combine(h.Root, "oracle-run-3f2a");
        Assert.True(Directory.Exists(areaDir)); // CreateArea wrote area.yaml

        var ok = h.Module.DeleteArea("oracle-run-3f2a", "tapestry-core:recall");

        Assert.True(ok);
        Assert.Null(h.World.GetRoom("oracle-run:oracle-run-3f2a-entry"));
        Assert.Null(h.World.GetRoom("oracle-run:oracle-run-3f2a-1_0_0"));
        Assert.DoesNotContain(mob, h.World.GetAllTrackedEntities());
        Assert.False(h.Areas.Contains("oracle-run-3f2a"));
        Assert.False(h.Oracles.Contains("oracle-run-3f2a:mobs"));
        Assert.False(h.Items.HasTemplate("oracle-run-3f2a:kit-p1-wield"));
        Assert.False(h.Consequences.Has("oracle-run:oracle-run-3f2a-entry", "looted"));
        Assert.False(Directory.Exists(areaDir));
        h.Cleanup();
    }

    [Fact]
    public void DeleteArea_EvacuatesTheCaller_BeforeRemovingRooms()
    {
        var h = AreaTeardownHarness.New();
        h.AddRoom("tapestry-core:recall", "tapestry-core");
        h.Module.CreateArea("oracle-run-3f2a", "the Ashen Hollow");
        h.AddRoom("oracle-run:oracle-run-3f2a-entry", "oracle-run-3f2a");
        var player = h.AddPlayer("oracle-run:oracle-run-3f2a-entry", "Alice");

        Assert.True(h.Module.DeleteArea("oracle-run-3f2a", "tapestry-core:recall"));
        Assert.Equal("tapestry-core:recall", player.LocationRoomId);
        Assert.Contains(player, h.World.GetRoom("tapestry-core:recall")!.Entities);
        h.Cleanup();
    }

    [Fact]
    public void DeleteArea_WhenRecallRoomMissing_AndPlayerInside_AbortsAndChangesNothing()
    {
        var h = AreaTeardownHarness.New();
        h.Module.CreateArea("oracle-run-3f2a", "the Ashen Hollow");
        h.AddRoom("oracle-run:oracle-run-3f2a-entry", "oracle-run-3f2a");
        h.AddPlayer("oracle-run:oracle-run-3f2a-entry", "Alice");

        Assert.False(h.Module.DeleteArea("oracle-run-3f2a", "tapestry-core:recall"));
        Assert.NotNull(h.World.GetRoom("oracle-run:oracle-run-3f2a-entry"));
        Assert.True(h.Areas.Contains("oracle-run-3f2a"));
        Assert.True(Directory.Exists(Path.Combine(h.Root, "oracle-run-3f2a")));
        h.Cleanup();
    }

    [Fact]
    public void DeleteArea_OnUnknownArea_ReturnsFalse()
    {
        var h = AreaTeardownHarness.New();
        Assert.False(h.Module.DeleteArea("no-such-area", "tapestry-core:recall"));
        h.Cleanup();
    }

    [Fact]
    public void DeleteArea_IsIdempotent()
    {
        var h = AreaTeardownHarness.New();
        h.Module.CreateArea("oracle-run-3f2a", "the Ashen Hollow");
        h.AddRoom("oracle-run:oracle-run-3f2a-entry", "oracle-run-3f2a");

        Assert.True(h.Module.DeleteArea("oracle-run-3f2a", "tapestry-core:recall"));
        Assert.False(h.Module.DeleteArea("oracle-run-3f2a", "tapestry-core:recall"));
        h.Cleanup();
    }

    [Fact]
    public void DeleteArea_DoesNotTouchASiblingAreaSharingThePackNamespace()
    {
        var h = AreaTeardownHarness.New();
        h.Module.CreateArea("oracle-run-3f2a", "the Ashen Hollow");
        h.Module.CreateArea("oracle-run-9c1d", "the Sunken Reach");
        h.AddRoom("oracle-run:oracle-run-3f2a-entry", "oracle-run-3f2a");
        h.AddRoom("oracle-run:oracle-run-9c1d-entry", "oracle-run-9c1d");
        h.Oracles.Register(new OracleTable { Id = "oracle-run-9c1d:mobs", Kind = "mobs" });
        h.Items.Register(new ItemTemplate { Id = "oracle-run-9c1d:loot-01", Name = "a ring" });

        Assert.True(h.Module.DeleteArea("oracle-run-3f2a", "tapestry-core:recall"));

        Assert.True(h.Areas.Contains("oracle-run-9c1d"));
        Assert.NotNull(h.World.GetRoom("oracle-run:oracle-run-9c1d-entry"));
        Assert.True(h.Oracles.Contains("oracle-run-9c1d:mobs"));
        Assert.True(h.Items.HasTemplate("oracle-run-9c1d:loot-01"));
        Assert.True(Directory.Exists(Path.Combine(h.Root, "oracle-run-9c1d")));
        h.Cleanup();
    }
}
