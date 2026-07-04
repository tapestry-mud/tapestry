using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Races;
using Tapestry.Engine.Registration;
using Tapestry.Scripting;
using Tapestry.Scripting.Interop;
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
        IPackManifestProvider packLoader,
        RaceRegistry? raceRegistry = null)
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
        return new PlayerInitModule(
            config, packLoader, persistence, accountService, raceRegistry ?? new RaceRegistry(),
            new VitalsService(new EventBus()),
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
    public async Task LoadSeedPlayers_SeedPlayerInScopedPackDir_CreatesPlayerAndAccount()
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

            module.LoadSeedPlayers();

            playerStore.Saved.Should().ContainSingle(d => d.Name == "Wanderer");
            accountStore.ExistsByEmail("wanderer@localhost").Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadSeedPlayers_RaceCommitsAtSeal_SeedPlayerGetsRacialFlags()
    {
        // Boot order pin: race registrations route through RegistrationPolicy and only
        // commit into RaceRegistry at Resolve() (the seal barrier). Seeding must therefore
        // run AFTER the seal (GameLoopService.StartAsync), not at module Configure —
        // otherwise the seed player is created without racial flags and the
        // PlayerSaveExists guard makes the deficient save permanent.
        var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var packDir = Path.Combine(tmpDir, "@tapestry", "example-pack");
        Directory.CreateDirectory(packDir);
        await File.WriteAllTextAsync(
            Path.Combine(packDir, "players.yaml"),
            "players:\n  - name: Durin\n    password: testpass123\n    player_race: dwarf\n");

        try
        {
            var raceRegistry = new RaceRegistry();
            var policy = new RegistrationPolicy(new PackDependencyGraph());
            policy.Record(new RegistrationCandidate(
                Kind: "race",
                Name: "dwarf",
                Owner: "@tapestry/example-pack",
                IsOverride: false,
                Commit: () => raceRegistry.Register(new RaceDefinition
                {
                    Id = "dwarf",
                    Name = "Dwarf",
                    RacialFlags = new List<string> { "infravision" },
                    PackName = "@tapestry/example-pack"
                }),
                SourceFile: "scripts/races.js",
                Line: 0));

            var config = new ServerConfig();
            var playerStore = new FakeAdminStore();
            var module = BuildModule(
                config, playerStore, new FakeAccountStore(),
                new FakePackManifestProvider(new PackManifest
                {
                    Name = "@tapestry/example-pack",
                    PackDirectory = packDir
                }),
                raceRegistry);

            // Bootstrap phase: module Configure runs pre-seal — it must NOT seed players.
            module.Configure();
            playerStore.Saved.Should().BeEmpty(
                "seeding at Configure runs before the registration seal and would read an empty race registry");

            // GameLoopService.StartAsync: seal first, then seed.
            policy.Resolve();
            module.LoadSeedPlayers();

            var saved = playerStore.Saved.Should().ContainSingle(d => d.Name == "Durin").Subject;
            saved.Tags.Should().Contain("infravision");
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
