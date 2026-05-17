using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Data;
using Tapestry.Engine.Persistence;
using Tapestry.Server.Persistence;

namespace Tapestry.Engine.Tests.Persistence;

public class FilePlayerStoreHashGuardTests : IDisposable
{
    private readonly string _tmpDir;
    private readonly FilePlayerStore _store;

    public FilePlayerStoreHashGuardTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tmpDir);

        var config = new ServerConfig
        {
            Persistence = new PersistenceSection { SavePath = _tmpDir }
        };
        _store = new FilePlayerStore(config, NullLogger<FilePlayerStore>.Instance);
    }

    public void Dispose()
    {
        Directory.Delete(_tmpDir, recursive: true);
    }

    [Fact]
    public void GetFilePath_PathTraversal_ThrowsArgumentException()
    {
        var act = () => _store.Exists("../etc/passwd");
        act.Should().Throw<ArgumentException>().WithMessage("*traversal*");
    }

    [Fact]
    public async Task SaveAsync_WritesFile()
    {
        var data = new PlayerSaveData { Name = "Carol", AccountId = Guid.NewGuid().ToString() };
        await _store.SaveAsync(data);

        var expectedPath = Path.Combine(_tmpDir, "players", "carol", "player.yaml");
        File.Exists(expectedPath).Should().BeTrue();
        var yaml = await File.ReadAllTextAsync(expectedPath);
        yaml.Should().Contain("Carol");
    }
}
