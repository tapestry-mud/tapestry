using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Races;
using Tapestry.Scripting;
using Tapestry.Server.Modules;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Server;

public class PlayerInitModuleTests
{
    private static PlayerInitModule CreateModule(ServerConfig config, FakeAdminStore store)
    {
        var registry = new PropertyRegistry();
        CommonProperties.Register(registry);
        var serializer = new PlayerSerializer(registry);
        var sessions = new SessionManager();
        var world = new World();
        var persistence = new PlayerPersistenceService(
            store, serializer, sessions, world,
            NullLogger<PlayerPersistenceService>.Instance);
        var accountStore = new FakeAccountStore();
        var accountService = new AccountService(accountStore);
        var packLoader = new FakePackManifestProvider();
        var raceRegistry = new RaceRegistry();
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver();
        var spawns = new SpawnManager(world, eventBus, lootResolver, new ItemRegistry());
        return new PlayerInitModule(
            config, packLoader, persistence, accountService, raceRegistry, spawns,
            NullLogger<PlayerInitModule>.Instance);
    }

    [Fact]
    public void Configure_AdminBlockPresent_CreatesAdminPlayer()
    {
        var config = new ServerConfig
        {
            Admin = new AdminSeedSection { Handle = "mallek", Password = "changeme" }
        };
        var store = new FakeAdminStore();

        CreateModule(config, store).Configure();

        store.Saved.Should().ContainSingle(d => d.Name == "mallek");
    }

    [Fact]
    public void Configure_HandleIsTodo_SkipsAdminCreation()
    {
        var config = new ServerConfig
        {
            Admin = new AdminSeedSection { Handle = "TODO", Password = "changeme" }
        };
        var store = new FakeAdminStore();

        CreateModule(config, store).Configure();

        store.Saved.Should().BeEmpty();
    }

    [Fact]
    public void Configure_AdminSaveAlreadyExists_SkipsAdminCreation()
    {
        var config = new ServerConfig
        {
            Admin = new AdminSeedSection { Handle = "mallek", Password = "changeme" }
        };
        var store = new FakeAdminStore();
        store.MarkExists("mallek");

        CreateModule(config, store).Configure();

        store.Saved.Should().BeEmpty();
    }

    [Fact]
    public void Configure_AdminBlockPresent_CreatedPlayerHasAdminRole()
    {
        var config = new ServerConfig
        {
            Admin = new AdminSeedSection { Handle = "mallek", Password = "changeme" }
        };
        var store = new FakeAdminStore();

        CreateModule(config, store).Configure();

        var saved = store.Saved.Should().ContainSingle().Subject;
        saved.Roles.Should().Contain("admin");
        Guid.TryParse(saved.AccountId, out var parsedId).Should().BeTrue();
        parsedId.Should().NotBe(Guid.Empty);
    }

    private class FakeAccountStore : IAccountStore
    {
        private readonly Dictionary<Guid, AccountSaveData> _byId = new();
        private readonly Dictionary<string, Guid> _byEmail = new(StringComparer.OrdinalIgnoreCase);

        public Task SaveAsync(AccountSaveData data)
        {
            _byId[data.Id] = data;
            _byEmail[data.Email] = data.Id;
            return Task.CompletedTask;
        }

        public Task<AccountSaveData?> LoadByIdAsync(Guid accountId) =>
            Task.FromResult(_byId.GetValueOrDefault(accountId));

        public Task<AccountSaveData?> LoadByEmailAsync(string email)
        {
            if (_byEmail.TryGetValue(email, out var id))
            {
                return LoadByIdAsync(id);
            }
            return Task.FromResult<AccountSaveData?>(null);
        }

        public Task DeleteAsync(Guid accountId) => Task.CompletedTask;
        public bool ExistsByEmail(string email) => _byEmail.ContainsKey(email);
    }

    private class FakeAdminStore : IPlayerStore
    {
        private readonly HashSet<string> _existing = new(StringComparer.OrdinalIgnoreCase);
        public List<PlayerSaveData> Saved { get; } = new();

        public void MarkExists(string name) => _existing.Add(name);

        public bool Exists(string playerName) => _existing.Contains(playerName);

        public Task SaveAsync(PlayerSaveData data)
        {
            Saved.Add(data);
            _existing.Add(data.Name);
            return Task.CompletedTask;
        }

        public Task<PlayerSaveData?> LoadAsync(string playerName) =>
            Task.FromResult<PlayerSaveData?>(null);

        public Task DeleteAsync(string playerName) => Task.CompletedTask;

        public IReadOnlyList<string> GetSupplementalFileTypes(string playerName)
        {
            return Array.Empty<string>();
        }
    }

    private class FakePackManifestProvider : IPackManifestProvider
    {
        public IReadOnlyList<PackManifest> LoadedPacks => new List<PackManifest>();
    }
}
