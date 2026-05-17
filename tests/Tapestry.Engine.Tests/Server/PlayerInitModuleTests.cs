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
        var packLoader = new FakePackManifestProvider();
        var raceRegistry = new RaceRegistry();
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver();
        var spawns = new SpawnManager(world, eventBus, lootResolver, new ItemRegistry());
        return new PlayerInitModule(
            config, packLoader, persistence, raceRegistry, spawns,
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
        // TODO(Task 10): password verification will be via AccountService once account creation is wired
        saved.AccountId.Should().Be(Guid.Empty.ToString());
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
    }

    private class FakePackManifestProvider : IPackManifestProvider
    {
        public IReadOnlyList<PackManifest> LoadedPacks => new List<PackManifest>();
    }
}
