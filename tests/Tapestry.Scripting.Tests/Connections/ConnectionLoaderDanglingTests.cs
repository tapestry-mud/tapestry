using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Scripting.Connections;
using Xunit;

namespace Tapestry.Scripting.Tests.Connections;

public class ConnectionLoaderDanglingTests : IDisposable
{
    private readonly World _world;
    private readonly string _tempRoot;
    private readonly string _connectionsDir;

    public ConnectionLoaderDanglingTests()
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

    private void WriteYaml(string filename, string yaml)
    {
        File.WriteAllText(Path.Combine(_connectionsDir, filename), yaml);
    }

    [Fact]
    public void Load_BothRoomsExist_DanglingIsEmpty()
    {
        AddRoom("pack:room-a");
        AddRoom("lf:room-b");
        WriteYaml("conn-1.yaml", """
            id: "conn-1"
            from:
              room: "pack:room-a"
              type: direction
              direction: east
            to:
              room: "lf:room-b"
              type: direction
              direction: west
            created_by: test
            created_at: "2026-01-01T00:00:00Z"
            """);

        var loader = new ConnectionLoader(_world, NullLogger<ConnectionLoader>.Instance, _connectionsDir);
        loader.Load();

        loader.Dangling.Should().BeEmpty();
        loader.Loaded.Should().HaveCount(1);
    }

    [Fact]
    public void Load_FromRoomMissing_RecordInDangling()
    {
        // "pack:gone" does NOT exist in the world.
        AddRoom("lf:room-b");
        WriteYaml("conn-1.yaml", """
            id: "conn-1"
            from:
              room: "pack:gone"
              type: direction
              direction: east
            to:
              room: "lf:room-b"
              type: direction
              direction: west
            created_by: test
            created_at: "2026-01-01T00:00:00Z"
            """);

        var loader = new ConnectionLoader(_world, NullLogger<ConnectionLoader>.Instance, _connectionsDir);
        loader.Load();

        loader.Dangling.Should().HaveCount(1);
        loader.Dangling[0].Id.Should().Be("conn-1");
        loader.Loaded.Should().BeEmpty();
    }

    [Fact]
    public void Load_ToRoomMissing_RecordInDangling()
    {
        AddRoom("pack:room-a");
        // "lf:gone" does NOT exist in the world.
        WriteYaml("conn-1.yaml", """
            id: "conn-1"
            from:
              room: "pack:room-a"
              type: direction
              direction: east
            to:
              room: "lf:gone"
              type: direction
              direction: west
            created_by: test
            created_at: "2026-01-01T00:00:00Z"
            """);

        var loader = new ConnectionLoader(_world, NullLogger<ConnectionLoader>.Instance, _connectionsDir);
        loader.Load();

        loader.Dangling.Should().HaveCount(1);
        loader.Dangling[0].Id.Should().Be("conn-1");
        loader.Loaded.Should().BeEmpty();
    }
}
