using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Scripting;
using Tapestry.Scripting.Connections;
using Tapestry.Scripting.Modules;
using Tapestry.Shared;

namespace Tapestry.Scripting.Tests.Modules;

public class ConnectionsModuleTests : IDisposable
{
    private readonly World _world;
    private readonly string _tempRoot;
    private readonly ConnectionLoader _loader;
    private readonly JintRuntime _runtime;

    public ConnectionsModuleTests()
    {
        _world = new World();
        _tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempRoot);

        _loader = new ConnectionLoader(_world, NullLogger<ConnectionLoader>.Instance, _tempRoot);

        var connectionsModule = new ConnectionsModule(_world, _loader, _tempRoot);
        var roomsModule = new RoomsModule(_world);
        var fsModule = new FsModule(_tempRoot);

        _runtime = new JintRuntime(
            new IJintApiModule[] { connectionsModule, roomsModule, fsModule },
            NullLogger<JintRuntime>.Instance
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private Room AddRoom(string id)
    {
        var room = new Room(id, id, "");
        var packName = id.Contains(':') ? id[..id.IndexOf(':')] : id;
        room.SetProperty("source_pack", packName);
        _world.AddRoom(room);
        return room;
    }

    // -----------------------------------------------------------------------
    // tapestry.connections.getAll()
    // -----------------------------------------------------------------------

    [Fact]
    public void GetAll_WhenNoConnectionsLoaded_ReturnsEmptyArray()
    {
        var count = _runtime.Evaluate("tapestry.connections.getAll().length");
        Convert.ToInt32(count).Should().Be(0);
    }

    // -----------------------------------------------------------------------
    // tapestry.connections.create(...)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_AddsExitsToBothRoomsAndAppendsToLoaded()
    {
        AddRoom("pack:room-a");
        AddRoom("pack:room-b");

        _runtime.Execute("""
            tapestry.connections.create(
                "pack:room-a", "direction", { direction: "north" },
                "pack:room-b", "direction", { direction: "south" }
            );
        """);

        var count = _runtime.Evaluate("tapestry.connections.getAll().length");
        Convert.ToInt32(count).Should().Be(1);

        var roomA = _world.GetRoom("pack:room-a")!;
        var roomB = _world.GetRoom("pack:room-b")!;
        roomA.GetExit(Direction.North).Should().NotBeNull();
        roomA.GetExit(Direction.North)!.TargetRoomId.Should().Be("pack:room-b");
        roomB.GetExit(Direction.South).Should().NotBeNull();
        roomB.GetExit(Direction.South)!.TargetRoomId.Should().Be("pack:room-a");

        _loader.Loaded.Should().HaveCount(1);
    }

    // -----------------------------------------------------------------------
    // tapestry.connections.getForRoom(roomId)
    // -----------------------------------------------------------------------

    [Fact]
    public void GetForRoom_ReturnsOnlyConnectionsTouchingThatRoom()
    {
        AddRoom("pack:room-a");
        AddRoom("pack:room-b");
        AddRoom("pack:room-c");

        _runtime.Execute("""
            tapestry.connections.create(
                "pack:room-a", "direction", { direction: "north" },
                "pack:room-b", "direction", { direction: "south" }
            );
            tapestry.connections.create(
                "pack:room-b", "direction", { direction: "east" },
                "pack:room-c", "direction", { direction: "west" }
            );
        """);

        var countA = _runtime.Evaluate("tapestry.connections.getForRoom('pack:room-a').length");
        var countB = _runtime.Evaluate("tapestry.connections.getForRoom('pack:room-b').length");
        var countC = _runtime.Evaluate("tapestry.connections.getForRoom('pack:room-c').length");

        Convert.ToInt32(countA).Should().Be(1);
        Convert.ToInt32(countB).Should().Be(2);
        Convert.ToInt32(countC).Should().Be(1);
    }

    // -----------------------------------------------------------------------
    // tapestry.connections.remove(id)
    // -----------------------------------------------------------------------

    [Fact]
    public void Remove_DeletesExitsAndRemovesFromLoaded()
    {
        AddRoom("pack:room-a");
        AddRoom("pack:room-b");

        _runtime.Execute("""
            var id = tapestry.connections.create(
                "pack:room-a", "direction", { direction: "north" },
                "pack:room-b", "direction", { direction: "south" }
            );
            tapestry.connections.remove(id);
        """);

        var count = _runtime.Evaluate("tapestry.connections.getAll().length");
        Convert.ToInt32(count).Should().Be(0);

        _loader.Loaded.Should().BeEmpty();

        var roomA = _world.GetRoom("pack:room-a")!;
        var roomB = _world.GetRoom("pack:room-b")!;
        roomA.GetExit(Direction.North).Should().BeNull();
        roomB.GetExit(Direction.South).Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // tapestry.rooms.getEntryPoints(packName)
    // -----------------------------------------------------------------------

    [Fact]
    public void GetEntryPoints_ReturnsOnlyRoomsWithEntryPointTag()
    {
        var entry = AddRoom("mypack:entry-room");
        entry.AddTag("entry-point");
        entry.SetProperty("entry_point_description", "The main gate.");
        entry.SetProperty("entry_point_direction", "east");

        var plain = AddRoom("mypack:plain-room");

        var otherPack = AddRoom("otherpack:room");
        otherPack.AddTag("entry-point");

        var count = _runtime.Evaluate("tapestry.rooms.getEntryPoints('mypack').length");
        Convert.ToInt32(count).Should().Be(1);

        var id = _runtime.Evaluate("tapestry.rooms.getEntryPoints('mypack')[0].id");
        id?.ToString().Should().Be("mypack:entry-room");

        var desc = _runtime.Evaluate("tapestry.rooms.getEntryPoints('mypack')[0].entry_point_description");
        desc?.ToString().Should().Be("The main gate.");

        var dir = _runtime.Evaluate("tapestry.rooms.getEntryPoints('mypack')[0].entry_point_direction");
        dir?.ToString().Should().Be("east");
    }

    // -----------------------------------------------------------------------
    // tapestry.rooms.getExits(roomId)
    // -----------------------------------------------------------------------

    [Fact]
    public void GetExits_IncludesBothOccupiedAndAvailableSlots()
    {
        var room = AddRoom("pack:room-exits");
        var target = AddRoom("pack:room-target");
        room.SetExit(Direction.North, new Exit("pack:room-target"));
        room.SetKeywordExit("ladder", new Exit("pack:room-target") { DisplayName = "a wooden ladder" });

        var allExits = _runtime.Evaluate("tapestry.rooms.getExits('pack:room-exits').length");
        Convert.ToInt32(allExits).Should().BeGreaterThan(0);

        var northOccupied = _runtime.Evaluate("""
            tapestry.rooms.getExits('pack:room-exits')
                .filter(function(e) { return e.type === 'direction' && e.direction === 'North'; })[0].occupied
        """);
        northOccupied?.ToString()?.ToLower().Should().Be("true");

        var southOccupied = _runtime.Evaluate("""
            tapestry.rooms.getExits('pack:room-exits')
                .filter(function(e) { return e.type === 'direction' && e.direction === 'South'; })[0].occupied
        """);
        southOccupied?.ToString()?.ToLower().Should().Be("false");

        var keywordCount = _runtime.Evaluate("""
            tapestry.rooms.getExits('pack:room-exits')
                .filter(function(e) { return e.type === 'keyword' && e.occupied === true; }).length
        """);
        Convert.ToInt32(keywordCount).Should().Be(1);
    }

    // -----------------------------------------------------------------------
    // tapestry.fs.writeYaml() - path traversal blocked
    // -----------------------------------------------------------------------

    [Fact]
    public void WriteYaml_ThrowsOnPathTraversalAttempt()
    {
        var act = () =>
        {
            _runtime.Execute("""
                tapestry.fs.writeYaml("../secret.yaml", { key: "value" });
            """);
        };

        act.Should().Throw<Exception>().WithMessage("*escapes*");
    }

    // -----------------------------------------------------------------------
    // tapestry.fs.writeYaml() - valid path succeeds and creates file
    // -----------------------------------------------------------------------

    [Fact]
    public void WriteYaml_ValidRelativePath_CreatesFile()
    {
        _runtime.Execute("""
            tapestry.fs.writeYaml("test.yaml", { name: "hello", count: 42 });
        """);

        var expectedPath = Path.Combine(_tempRoot, "connections", "test.yaml");
        File.Exists(expectedPath).Should().BeTrue();

        var content = File.ReadAllText(expectedPath);
        content.Should().Contain("hello");
    }
}
