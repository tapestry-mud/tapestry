using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Scripting.Connections;

namespace Tapestry.Scripting.Tests.Connections;

public class ConnectionLoaderRetargetTests : IDisposable
{
    private readonly World _world = new();
    private readonly string _connectionsDir;
    private readonly ConnectionLoader _loader;

    public ConnectionLoaderRetargetTests()
    {
        _connectionsDir = Path.Combine(Path.GetTempPath(), "conn-retarget-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_connectionsDir);
        _loader = new ConnectionLoader(_world, NullLogger<ConnectionLoader>.Instance, _connectionsDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_connectionsDir))
        {
            Directory.Delete(_connectionsDir, recursive: true);
        }
    }

    private ConnectionRecord AddRecord(string id, string fromRoom, string toRoom)
    {
        var record = new ConnectionRecord
        {
            Id = id,
            From = new ConnectionSide { Room = fromRoom, Type = "direction", Direction = "north" },
            To = new ConnectionSide { Room = toRoom, Type = "direction", Direction = "south" },
            CreatedBy = "test",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _loader.AddLoaded(record);
        // Simulate the on-disk file that ConnectionsModule.create would have written.
        File.WriteAllText(Path.Combine(_connectionsDir, $"{id}.yaml"), "placeholder");
        return record;
    }

    [Fact]
    public void RetargetRoom_UpdatesFromSide_AndRewritesFile()
    {
        AddRecord("rec-1", "ns:old-room", "lf:cellar");

        var affected = _loader.RetargetRoom("ns:old-room", "ns:gatehouse");

        affected.Should().ContainSingle().Which.Id.Should().Be("rec-1");
        affected[0].From.Room.Should().Be("ns:gatehouse");
        affected[0].To.Room.Should().Be("lf:cellar");

        var yaml = File.ReadAllText(Path.Combine(_connectionsDir, "rec-1.yaml"));
        yaml.Should().Contain("ns:gatehouse");
        yaml.Should().NotContain("ns:old-room");
    }

    [Fact]
    public void RetargetRoom_UpdatesToSide()
    {
        AddRecord("rec-2", "lf:cellar", "ns:old-room");

        var affected = _loader.RetargetRoom("ns:old-room", "ns:gatehouse");

        affected.Should().ContainSingle();
        affected[0].To.Room.Should().Be("ns:gatehouse");
        affected[0].From.Room.Should().Be("lf:cellar");
    }

    [Fact]
    public void RetargetRoom_LeavesUnrelatedRecordsAlone()
    {
        AddRecord("rec-3", "lf:cellar", "lf:hollow-square");
        File.WriteAllText(Path.Combine(_connectionsDir, "rec-3.yaml"), "untouched-marker");

        var affected = _loader.RetargetRoom("ns:old-room", "ns:gatehouse");

        affected.Should().BeEmpty();
        File.ReadAllText(Path.Combine(_connectionsDir, "rec-3.yaml")).Should().Be("untouched-marker");
    }

    [Fact]
    public void RetargetRoom_RewrittenFile_RoundTripsThroughLoad()
    {
        // The rewritten YAML must be loadable by a fresh loader (serializer/deserializer parity).
        _world.AddRoom(new Room("ns:gatehouse", "Gatehouse", "d"));
        _world.AddRoom(new Room("lf:cellar", "Cellar", "d"));
        AddRecord("rec-rt", "ns:old-room", "lf:cellar");

        _loader.RetargetRoom("ns:old-room", "ns:gatehouse");

        var freshLoader = new ConnectionLoader(_world, NullLogger<ConnectionLoader>.Instance, _connectionsDir);
        freshLoader.Load();

        var reloaded = freshLoader.Loaded.Should().ContainSingle().Subject;
        reloaded.From.Room.Should().Be("ns:gatehouse");
        reloaded.To.Room.Should().Be("lf:cellar");
    }
}
