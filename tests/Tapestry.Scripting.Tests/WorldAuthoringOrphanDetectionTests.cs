using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Scripting.Connections;
using Tapestry.Scripting.Modules;
using Xunit;

namespace Tapestry.Scripting.Tests;

public class WorldAuthoringOrphanDetectionTests : IDisposable
{
    private readonly World _world;
    private readonly string _root;
    private readonly string _connectionsDir;
    private readonly ConnectionLoader _connections;
    private readonly WorldAuthoringModule _mod;

    public WorldAuthoringOrphanDetectionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "orphan-" + Path.GetRandomFileName());
        _connectionsDir = Path.Combine(_root, "connections");
        Directory.CreateDirectory(_connectionsDir);
        _world = new World();
        var props = new PropertyRegistry();
        var tags = new TagRegistry();
        var projector = new RoomProjector(_world, props, tags, new AreaRegistry());
        var writer = new AttributeWriter(props, tags);
        _connections = new ConnectionLoader(
            _world, NullLogger<ConnectionLoader>.Instance, _connectionsDir);
        _mod = new WorldAuthoringModule(
            _world, projector, writer, _root,
            new HashSet<string> { "legends-forgotten" },
            new AreaRegistry(), new StubExitResolver(), recommend: null, connections: _connections);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    // Writes a direction connection YAML and calls Load() so the real Load path runs.
    private void WriteAndLoad(string fromRoom, string toRoom)
    {
        File.WriteAllText(Path.Combine(_connectionsDir, "conn-1.yaml"),
            $"""
            id: "conn-1"
            from:
              room: "{fromRoom}"
              type: direction
              direction: east
            to:
              room: "{toRoom}"
              type: direction
              direction: west
            created_by: test
            created_at: "2026-01-01T00:00:00Z"
            """);
        _connections.Load();
    }

    [Fact]
    public void GetAreaRooms_NoFlag_WhenConnectionCounterpartExists()
    {
        // Both rooms in world: Load() succeeds, record goes to Loaded not Dangling.
        _mod.CreateRoom("lf-area", "legends-forgotten:ext-0", "Ext", "d");
        var packRoom = new Room("lf:anchor", "Anchor", "") { Area = "lf-area" };
        packRoom.SetProperty("source_pack", "lf");
        _world.AddRoom(packRoom);
        WriteAndLoad("lf:anchor", "legends-forgotten:ext-0");

        var rooms = _mod.GetAreaRooms("lf-area");

        var ext = rooms.First(r => r.Id == "legends-forgotten:ext-0");
        ext.Provenance.Should().NotContain("orphaned");
    }

    [Fact]
    public void GetAreaRooms_FlagsOrphan_WhenCounterpartMissing()
    {
        // "lf:gone-anchor" not in world: Load() fails, record goes to Dangling.
        _mod.CreateRoom("lf-area", "legends-forgotten:ext-0", "Ext", "d");
        WriteAndLoad("lf:gone-anchor", "legends-forgotten:ext-0");

        var rooms = _mod.GetAreaRooms("lf-area");

        var ext = rooms.First(r => r.Id == "legends-forgotten:ext-0");
        ext.Provenance.Should().Contain("orphaned");
    }

    [Fact]
    public void GetAreaRooms_NeverFlagsPackRooms()
    {
        // Pack room exists; its counterpart does not. Pack rooms skip the orphan check.
        var packRoom = new Room("lf:anchor", "Anchor", "") { Area = "lf-area" };
        packRoom.SetProperty("source_pack", "lf");
        _world.AddRoom(packRoom);
        WriteAndLoad("lf:anchor", "lf:nowhere");

        var rooms = _mod.GetAreaRooms("lf-area");

        var anchor = rooms.First(r => r.Id == "lf:anchor");
        anchor.Provenance.Should().NotContain("orphaned");
    }

    [Fact]
    public void GetAreaRooms_OrphanedProvenanceString_Matches()
    {
        _mod.CreateRoom("lf-area", "legends-forgotten:ext-0", "Ext", "d");
        WriteAndLoad("lf:gone-anchor", "legends-forgotten:ext-0");

        var rooms = _mod.GetAreaRooms("lf-area");

        var ext = rooms.First(r => r.Id == "legends-forgotten:ext-0");
        ext.Provenance.Should().Be("[authored] (orphaned)");
    }
}
