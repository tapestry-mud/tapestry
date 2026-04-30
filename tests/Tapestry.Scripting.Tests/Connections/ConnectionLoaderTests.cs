using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Scripting.Connections;
using Tapestry.Shared;

namespace Tapestry.Scripting.Tests.Connections;

public class ConnectionLoaderTests : IDisposable
{
    private readonly World _world;
    private readonly string _tempRoot;
    private readonly string _connectionsDir;

    public ConnectionLoaderTests()
    {
        _world = new World();
        _tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _connectionsDir = Path.Combine(_tempRoot, "connections");
        Directory.CreateDirectory(_connectionsDir);
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
        _world.AddRoom(room);
        return room;
    }

    private ConnectionLoader CreateLoader()
    {
        return new ConnectionLoader(_world, NullLogger<ConnectionLoader>.Instance, _tempRoot);
    }

    private void WriteYaml(string filename, string yaml)
    {
        File.WriteAllText(Path.Combine(_connectionsDir, filename), yaml);
    }

    // Test 1: Valid direction connection creates exits on both rooms
    [Fact]
    public void Load_ValidDirectionConnection_CreatesExitsOnBothRooms()
    {
        var roomA = AddRoom("test-pack:room-a");
        var roomB = AddRoom("test-pack:room-b");

        WriteYaml("conn-ab.yaml", """
            id: "test-pack_room-a--test-pack_room-b"
            from:
              room: "test-pack:room-a"
              type: direction
              direction: north
            to:
              room: "test-pack:room-b"
              type: direction
              direction: south
            created_by: TestAdmin
            created_at: "2026-04-29T19:00:00Z"
            """);

        var loader = CreateLoader();
        loader.Load();

        roomA.GetExit(Direction.North).Should().NotBeNull();
        roomA.GetExit(Direction.North)!.TargetRoomId.Should().Be("test-pack:room-b");
        roomB.GetExit(Direction.South).Should().NotBeNull();
        roomB.GetExit(Direction.South)!.TargetRoomId.Should().Be("test-pack:room-a");
    }

    // Test 2: Valid keyword connection creates keyword exits on both rooms
    [Fact]
    public void Load_ValidKeywordConnection_CreatesKeywordExitsOnBothRooms()
    {
        var roomA = AddRoom("test-pack:room-a");
        var roomB = AddRoom("test-pack:room-b");

        WriteYaml("conn-kw.yaml", """
            id: "test-pack_room-a--test-pack_room-b-kw"
            from:
              room: "test-pack:room-a"
              type: keyword
              keyword: portal
              display_name: "a shimmering portal"
            to:
              room: "test-pack:room-b"
              type: keyword
              keyword: exit
              display_name: "a glowing exit"
            created_by: TestAdmin
            created_at: "2026-04-29T19:00:00Z"
            """);

        var loader = CreateLoader();
        loader.Load();

        roomA.HasKeywordExit("portal").Should().BeTrue();
        roomA.GetKeywordExit("portal")!.TargetRoomId.Should().Be("test-pack:room-b");
        roomB.HasKeywordExit("exit").Should().BeTrue();
        roomB.GetKeywordExit("exit")!.TargetRoomId.Should().Be("test-pack:room-a");
    }

    // Test 3: One-way connection creates exit only on From side
    [Fact]
    public void Load_OneWayConnection_CreatesExitOnlyOnFromSide()
    {
        var roomA = AddRoom("test-pack:room-a");
        var roomB = AddRoom("test-pack:room-b");

        WriteYaml("conn-oneway.yaml", """
            id: "test-pack_room-a--test-pack_room-b-ow"
            from:
              room: "test-pack:room-a"
              type: direction
              direction: east
            to:
              room: "test-pack:room-b"
              type: one-way
            created_by: TestAdmin
            created_at: "2026-04-29T19:00:00Z"
            """);

        var loader = CreateLoader();
        loader.Load();

        roomA.GetExit(Direction.East).Should().NotBeNull();
        roomA.GetExit(Direction.East)!.TargetRoomId.Should().Be("test-pack:room-b");
        roomB.GetExit(Direction.West).Should().BeNull();
        roomB.GetExit(Direction.East).Should().BeNull();
    }

    // Test 4: Room not found logs warning and skips entire connection
    [Fact]
    public void Load_RoomNotFound_SkipsEntireConnection_NoExitsCreated()
    {
        var roomA = AddRoom("test-pack:room-a");
        // "test-pack:room-missing" is not added

        WriteYaml("conn-missing.yaml", """
            id: "test-pack_room-a--test-pack_room-missing"
            from:
              room: "test-pack:room-a"
              type: direction
              direction: north
            to:
              room: "test-pack:room-missing"
              type: direction
              direction: south
            created_by: TestAdmin
            created_at: "2026-04-29T19:00:00Z"
            """);

        var loader = CreateLoader();
        loader.Load();

        roomA.GetExit(Direction.North).Should().BeNull();
        loader.Loaded.Should().BeEmpty();
    }

    // Test 5: Direction already occupied skips that side, applies other side
    [Fact]
    public void Load_DirectionAlreadyOccupied_SkipsThatSide_AppliesOtherSide()
    {
        var roomA = AddRoom("test-pack:room-a");
        var roomB = AddRoom("test-pack:room-b");
        // Pre-occupy north on roomA
        roomA.SetExit(Direction.North, new Exit("test-pack:other-room"));

        WriteYaml("conn-occupied.yaml", """
            id: "test-pack_room-a--test-pack_room-b-occ"
            from:
              room: "test-pack:room-a"
              type: direction
              direction: north
            to:
              room: "test-pack:room-b"
              type: direction
              direction: south
            created_by: TestAdmin
            created_at: "2026-04-29T19:00:00Z"
            """);

        var loader = CreateLoader();
        loader.Load();

        // From side is blocked - should remain pointing at other-room
        roomA.GetExit(Direction.North)!.TargetRoomId.Should().Be("test-pack:other-room");
        // To side should still be applied
        roomB.GetExit(Direction.South).Should().NotBeNull();
        roomB.GetExit(Direction.South)!.TargetRoomId.Should().Be("test-pack:room-a");
    }

    // Test 6: Duplicate (From.Room, To.Room) pair skips second file
    [Fact]
    public void Load_DuplicateRoomPair_SkipsSecondFile()
    {
        var roomA = AddRoom("test-pack:room-a");
        var roomB = AddRoom("test-pack:room-b");

        // First file: north/south
        WriteYaml("aaa-conn.yaml", """
            id: "test-pack_room-a--test-pack_room-b-first"
            from:
              room: "test-pack:room-a"
              type: direction
              direction: north
            to:
              room: "test-pack:room-b"
              type: direction
              direction: south
            created_by: TestAdmin
            created_at: "2026-04-29T19:00:00Z"
            """);

        // Second file: same room pair - should be skipped
        WriteYaml("zzz-conn.yaml", """
            id: "test-pack_room-a--test-pack_room-b-second"
            from:
              room: "test-pack:room-a"
              type: direction
              direction: up
            to:
              room: "test-pack:room-b"
              type: direction
              direction: down
            created_by: TestAdmin
            created_at: "2026-04-29T19:00:00Z"
            """);

        var loader = CreateLoader();
        loader.Load();

        // Only the first connection should be loaded
        loader.Loaded.Should().HaveCount(1);
        roomA.GetExit(Direction.North).Should().NotBeNull();
        roomA.GetExit(Direction.Up).Should().BeNull();
    }

    // Test 7: Empty/missing connections/ directory is a no-op, no exception
    [Fact]
    public void Load_MissingConnectionsDirectory_IsNoOpNoException()
    {
        // Use a temp root with no connections/ subdirectory
        var emptyRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(emptyRoot);

        try
        {
            var loader = new ConnectionLoader(_world, NullLogger<ConnectionLoader>.Instance, emptyRoot);

            var act = () => { loader.Load(); };

            act.Should().NotThrow();
            loader.Loaded.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(emptyRoot))
            {
                Directory.Delete(emptyRoot, recursive: true);
            }
        }
    }

    // Test 8: Loaded list contains only successfully created connections
    [Fact]
    public void Load_LoadedList_ContainsOnlySuccessfulConnections()
    {
        var roomA = AddRoom("test-pack:room-a");
        var roomB = AddRoom("test-pack:room-b");
        // roomC intentionally missing

        // Good connection
        WriteYaml("aaa-good.yaml", """
            id: "test-pack_room-a--test-pack_room-b"
            from:
              room: "test-pack:room-a"
              type: direction
              direction: north
            to:
              room: "test-pack:room-b"
              type: direction
              direction: south
            created_by: TestAdmin
            created_at: "2026-04-29T19:00:00Z"
            """);

        // Bad connection (room-c not found)
        WriteYaml("zzz-bad.yaml", """
            id: "test-pack_room-a--test-pack_room-c"
            from:
              room: "test-pack:room-a"
              type: direction
              direction: east
            to:
              room: "test-pack:room-c"
              type: direction
              direction: west
            created_by: TestAdmin
            created_at: "2026-04-29T19:00:00Z"
            """);

        var loader = CreateLoader();
        loader.Load();

        loader.Loaded.Should().HaveCount(1);
        loader.Loaded[0].Id.Should().Be("test-pack_room-a--test-pack_room-b");
    }
}
