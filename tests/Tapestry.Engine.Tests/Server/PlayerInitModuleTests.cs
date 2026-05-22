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
    private static PlayerInitModule CreateModule(ServerConfig config, FakeAdminStore store) =>
        BuildModule(config, store, new FakeAccountStore(), new FakePackManifestProvider());

    private static PlayerInitModule BuildModule(
        ServerConfig config,
        IPlayerStore playerStore,
        IAccountStore accountStore,
        IPackManifestProvider packLoader)
    {
        var registry = new PropertyRegistry();
        CommonProperties.Register(registry);
        var serializer = new PlayerSerializer(registry);
        var sessions = new SessionManager();
        var world = new World();
        var persistence = new PlayerPersistenceService(
            playerStore, serializer, sessions, world,
            NullLogger<PlayerPersistenceService>.Instance);
        var accountService = new AccountService(accountStore);
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

    [Fact]
    public async Task Configure_SeedPlayerInScopedPackDir_CreatesPlayerAndAccount()
    {
        // Seed players live in a scoped pack directory (@scope/name). The loader
        // must read players.yaml from the loaded pack's actual directory, not a
        // reconstructed bin/packs/<bareName> path.
        var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var packDir = Path.Combine(tmpDir, "@tapestry", "example-pack");
        Directory.CreateDirectory(packDir);
        await File.WriteAllTextAsync(
            Path.Combine(packDir, "players.yaml"),
            "players:\n  - name: Wanderer\n    password: testpass123\n");

        try
        {
            var config = new ServerConfig();
            var playerStore = new FakeAdminStore();
            var accountStore = new FakeAccountStore();
            var module = BuildModule(
                config, playerStore, accountStore,
                new FakePackManifestProvider(new PackManifest
                {
                    Name = "@tapestry/example-pack",
                    PackDirectory = packDir
                }));

            module.Configure();

            playerStore.Saved.Should().ContainSingle(d => d.Name == "Wanderer");
            accountStore.ExistsByEmail("wanderer@localhost").Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
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
        private readonly List<PackManifest> _packs;
        public FakePackManifestProvider(params PackManifest[] packs) => _packs = packs.ToList();
        public IReadOnlyList<PackManifest> LoadedPacks => _packs;
    }
}
