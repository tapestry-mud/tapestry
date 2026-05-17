// tests/Tapestry.Engine.Tests/Persistence/FileAccountStoreTests.cs
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Data;
using Tapestry.Engine.Persistence;
using Tapestry.Server.Persistence;

namespace Tapestry.Engine.Tests.Persistence;

public class FileAccountStoreTests : IDisposable
{
    private readonly string _tmpDir;
    private readonly FileAccountStore _store;

    public FileAccountStoreTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tmpDir);

        var config = new ServerConfig
        {
            Persistence = new PersistenceSection { SavePath = _tmpDir }
        };
        _store = new FileAccountStore(config, NullLogger<FileAccountStore>.Instance);
    }

    public void Dispose()
    {
        Directory.Delete(_tmpDir, recursive: true);
    }

    private AccountSaveData MakeAccount(string email, params string[] characters)
    {
        var account = new AccountSaveData
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "$2a$12$fakehash",
            Characters = characters.ToList()
        };
        return account;
    }

    [Fact]
    public async Task SaveAndLoadById_RoundTrips()
    {
        var account = MakeAccount("test@example.com", "mallek");
        await _store.SaveAsync(account);

        var loaded = await _store.LoadByIdAsync(account.Id);
        loaded.Should().NotBeNull();
        loaded!.Email.Should().Be("test@example.com");
        loaded.Characters.Should().ContainSingle().Which.Should().Be("mallek");
    }

    [Fact]
    public async Task LoadByEmail_UsesIndex()
    {
        var account = MakeAccount("lookup@test.com", "siron");
        await _store.SaveAsync(account);

        var loaded = await _store.LoadByEmailAsync("lookup@test.com");
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(account.Id);
    }

    [Fact]
    public async Task ExistsByEmail_ReturnsTrueAfterSave()
    {
        _store.ExistsByEmail("new@test.com").Should().BeFalse();

        var account = MakeAccount("new@test.com");
        await _store.SaveAsync(account);

        _store.ExistsByEmail("new@test.com").Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_RemovesAccountAndIndex()
    {
        var account = MakeAccount("delete@test.com");
        await _store.SaveAsync(account);

        await _store.DeleteAsync(account.Id);

        var loaded = await _store.LoadByIdAsync(account.Id);
        loaded.Should().BeNull();
        _store.ExistsByEmail("delete@test.com").Should().BeFalse();
    }

    [Fact]
    public async Task LoadByEmail_NonExistent_ReturnsNull()
    {
        var loaded = await _store.LoadByEmailAsync("nobody@nowhere.com");
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task LoadById_NonExistent_ReturnsNull()
    {
        var loaded = await _store.LoadByIdAsync(Guid.NewGuid());
        loaded.Should().BeNull();
    }
}
