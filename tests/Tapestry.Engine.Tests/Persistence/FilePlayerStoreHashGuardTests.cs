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

    private static PlayerSaveData MakeData(string name, string hash)
    {
        return new PlayerSaveData { Name = name, PasswordHash = hash };
    }

    [Fact]
    public async Task SaveAsync_NullHash_ThrowsInvalidOperationException()
    {
        var data = MakeData("Alice", null!);
        var act = async () => await _store.SaveAsync(data);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Alice*");
    }

    [Fact]
    public async Task SaveAsync_EmptyHash_ThrowsInvalidOperationException()
    {
        var data = MakeData("Bob", "");
        var act = async () => await _store.SaveAsync(data);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Bob*");
    }

    [Fact]
    public async Task SaveAsync_ValidHash_WritesFile()
    {
        var data = MakeData("Carol", "$2a$12$validhash");
        await _store.SaveAsync(data);

        var expectedPath = Path.Combine(_tmpDir, "players", "carol.yaml");
        File.Exists(expectedPath).Should().BeTrue();
        var yaml = await File.ReadAllTextAsync(expectedPath);
        yaml.Should().Contain("Carol");
    }
}
