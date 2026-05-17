using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Data;
using Tapestry.Engine.Persistence;
using Tapestry.Server.Persistence;

namespace Tapestry.Engine.Tests.Persistence;

public class FilePlayerStoreDirectoryTests : IDisposable
{
    private readonly string _tmpDir;
    private readonly FilePlayerStore _store;

    public FilePlayerStoreDirectoryTests()
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
    public async Task SaveAsync_CreatesDirectoryLayout()
    {
        var data = new PlayerSaveData
        {
            Name = "Mallek",
            AccountId = Guid.NewGuid().ToString()
        };
        await _store.SaveAsync(data);

        var expectedPath = Path.Combine(_tmpDir, "players", "mallek", "player.yaml");
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_ReadsFromDirectory()
    {
        var data = new PlayerSaveData
        {
            Name = "Mallek",
            AccountId = Guid.NewGuid().ToString()
        };
        await _store.SaveAsync(data);

        var loaded = await _store.LoadAsync("mallek");
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Mallek");
    }

    [Fact]
    public async Task Exists_ReturnsTrueForSavedPlayer()
    {
        var data = new PlayerSaveData
        {
            Name = "TestPlayer",
            AccountId = Guid.NewGuid().ToString()
        };
        await _store.SaveAsync(data);

        _store.Exists("testplayer").Should().BeTrue();
        _store.Exists("nobody").Should().BeFalse();
    }

    [Fact]
    public async Task GetSupplementalFileTypes_ReturnsQuestsWhenPresent()
    {
        var data = new PlayerSaveData
        {
            Name = "QuestHaver",
            AccountId = Guid.NewGuid().ToString()
        };
        await _store.SaveAsync(data);

        // Manually create a quests.yaml sidecar
        var questsPath = Path.Combine(_tmpDir, "players", "questhaver", "quests.yaml");
        await File.WriteAllTextAsync(questsPath, "active: []\ncompleted: []\n");

        var types = _store.GetSupplementalFileTypes("questhaver");
        types.Should().Contain("quests");
    }

    [Fact]
    public void GetSupplementalFileTypes_ReturnsEmptyForNoExtras()
    {
        var types = _store.GetSupplementalFileTypes("nobody");
        types.Should().BeEmpty();
    }

    [Fact]
    public void GetFilePath_PathTraversal_ThrowsArgumentException()
    {
        var act = () => _store.Exists("../etc/passwd");
        act.Should().Throw<ArgumentException>().WithMessage("*traversal*");
    }
}
