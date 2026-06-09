using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Color;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Login;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Races;
using Tapestry.Engine.Ui;
using Tapestry.Server;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Server;

public class ConnectionHandlerLoginPhaseTests
{
    // ---- Fakes ----

    private class FakeGmcpHandler : IGmcpHandler
    {
        public bool GmcpActive { get; set; } = true;
        public List<(string Package, object Payload)> Sent { get; } = new();
        public Action<string, JsonElement>? OnGmcpMessage { get; set; }

        public void Send(string package, object payload)
        {
            Sent.Add((package, payload));
        }

        public bool SupportsPackage(string package) => true;
    }

    private class FakePlayerStore : IPlayerStore
    {
        private readonly Dictionary<string, PlayerSaveData> _data = new(StringComparer.OrdinalIgnoreCase);

        public void Seed(PlayerSaveData data)
        {
            _data[data.Name] = data;
        }

        public Task SaveAsync(PlayerSaveData data)
        {
            _data[data.Name] = data;
            return Task.CompletedTask;
        }

        public Task<PlayerSaveData?> LoadAsync(string playerName)
        {
            _data.TryGetValue(playerName, out var d);
            return Task.FromResult(d);
        }

        public bool Exists(string playerName)
        {
            return _data.ContainsKey(playerName);
        }

        public Task DeleteAsync(string playerName)
        {
            _data.Remove(playerName);
            return Task.CompletedTask;
        }

        public IReadOnlyList<string> GetSupplementalFileTypes(string playerName)
        {
            return Array.Empty<string>();
        }
    }

    private class FakeFlowPersistence : IFlowPersistence
    {
        public bool PlayerExists(string name) => false;
        public void SaveNewPlayer(Entity entity, Guid accountId) { }
    }

    // ---- Harness ----

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

        public Task<AccountSaveData?> LoadByIdAsync(Guid accountId)
        {
            return Task.FromResult(_byId.GetValueOrDefault(accountId));
        }

        public Task<AccountSaveData?> LoadByEmailAsync(string email)
        {
            if (_byEmail.TryGetValue(email, out var id))
            {
                return LoadByIdAsync(id);
            }
            return Task.FromResult<AccountSaveData?>(null);
        }

        public Task DeleteAsync(Guid accountId)
        {
            if (_byId.TryGetValue(accountId, out var data))
            {
                _byEmail.Remove(data.Email);
                _byId.Remove(accountId);
            }
            return Task.CompletedTask;
        }

        public bool ExistsByEmail(string email)
        {
            return _byEmail.ContainsKey(email);
        }
    }

    private record Harness(
        ConnectionHandler Handler,
        FakeGmcpHandler GmcpHandler,
        FakeConnection Connection,
        FakePlayerStore Store,
        AccountService AccountService,
        GameLoop GameLoop);

    private static Harness Build(Action<FakePlayerStore>? seed = null, Action<AccountService>? accountSetup = null)
    {
        var store = new FakePlayerStore();
        seed?.Invoke(store);

        var accountStore = new FakeAccountStore();
        var accountService = new AccountService(accountStore);
        accountSetup?.Invoke(accountService);

        var sessions = new SessionManager();
        var playerCreator = new PlayerCreator();
        var world = new World(playerCreator);
        var registry = new PropertyRegistry();
        CommonProperties.Register(registry);
        var serializer = new PlayerSerializer(registry);
        var persistence = new PlayerPersistenceService(
            store, serializer, sessions, world,
            NullLogger<PlayerPersistenceService>.Instance);
        var eventBus = new EventBus();
        var alignmentConfig = new AlignmentConfig();
        var alignmentManager = new AlignmentManager(world, eventBus, alignmentConfig);
        var connectionManager = new Tapestry.Server.Gmcp.GmcpConnectionManager(sessions);
        var orchestrator = new Tapestry.Server.Gmcp.PostLoginOrchestrator(
            Array.Empty<Tapestry.Contracts.IGmcpPackageHandler>());
        var loginHandler = new Tapestry.Server.Gmcp.Handlers.LoginHandler(
            connectionManager, sessions, world, eventBus, orchestrator);
        var flowEngine = new FlowEngine(
            new FlowRegistry(),
            sessions,
            world,
            new FakeFlowPersistence(),
            new PanelRenderer(),
            new ClassRegistry(),
            new RaceRegistry(),
            alignmentManager,
            playerCreator,
            eventBus);

        var config = new ServerConfig
        {
            Persistence = new PersistenceSection
            {
                MaxLoginAttempts = 3,
                PasswordMinLength = 6
            }
        };

        var gameLoop = new GameLoop(
            new CommandRouter(new CommandRegistry(), sessions, world),
            sessions, new EventBus(), new SystemEventQueue(),
            NullLogger<GameLoop>.Instance,
            new TapestryMetrics(), new TickTimer(10), new NotificationQueue());

        var mobAI = new MobAIManager(world, eventBus,
            new CombatManager(world, eventBus),
            new DispositionEvaluator(world, eventBus, new AlignmentManager(world, eventBus, alignmentConfig)),
            NullLogger<MobAIManager>.Instance, new TapestryMetrics());

        var spawner = new PlayerSpawner(
            sessions, world, gameLoop, new TickTimer(10), config, loginHandler,
            mobAI, new SystemEventQueue(), new EventBus(), accountService,
            new TapestryMetrics(),
            NullLogger<PlayerSpawner>.Instance);

        var handler = new ConnectionHandler(
            sessions,
            new TapestryMetrics(),
            persistence,
            accountService,
            config,
            NullLogger<ConnectionHandler>.Instance,
            NullLogger<Tapestry.Server.Login.LoginFlow>.Instance,
            flowEngine,
            new ColorRenderer(new ThemeRegistry()),
            new Tapestry.Engine.Text.OutputWrapper(),
            new Tapestry.Engine.Text.OutputWidthService(new Tapestry.Data.ServerConfig()),
            new LoginGateRegistry(),
            connectionManager,
            loginHandler,
            spawner,
            new Tapestry.Engine.Watch.WatchRegistry());

        var conn = new FakeConnection();
        var gmcpHandler = new FakeGmcpHandler();

        return new Harness(handler, gmcpHandler, conn, store, accountService, gameLoop);
    }

    private static (PlayerSaveData Data, Guid AccountId) MakeSaveDataWithAccount(
        string name, string password, AccountService accountService)
    {
        var account = accountService.CreateAccount($"{name.ToLower()}@test.com", password)
            .GetAwaiter().GetResult();
        accountService.AddCharacterToAccount(account.Id, name).GetAwaiter().GetResult();

        var registry = new PropertyRegistry();
        CommonProperties.Register(registry);
        var serializer = new PlayerSerializer(registry);

        var entity = new Entity("player", name);
        entity.LocationRoomId = "core:town-square";

        return (serializer.ToSaveData(entity, account.Id, new List<Entity>()), account.Id);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 10000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(10);
        }
    }

    private static bool HasPhase(FakeGmcpHandler gmcp, string phase) =>
        gmcp.Sent.Any(x => x.Package == "Char.Login.Phase" &&
            x.Payload.GetType().GetProperty("phase")!.GetValue(x.Payload)!.ToString() == phase);

    // ---- Tests: existing player login ----

    [Fact]
    public async Task ExistingPlayerLogin_SendsNamePhaseOnConnect()
    {
        var h = Build();
        var (saveData, _) = MakeSaveDataWithAccount("Alice", "hunter2", h.AccountService);
        h.Store.Seed(saveData);

        h.Handler.HandleNewConnection(h.Connection, h.GmcpHandler);
        await WaitUntilAsync(() => HasPhase(h.GmcpHandler, "name"));

        h.GmcpHandler.Sent.Should().Contain(x => x.Package == "Char.Login.Phase");
        var namePhase = h.GmcpHandler.Sent.First(x => x.Package == "Char.Login.Phase");
        namePhase.Payload.Should().BeEquivalentTo(new { phase = "name" });
    }

    [Fact]
    public async Task ExistingPlayerLogin_SendsPasswordPhaseBeforePasswordPrompt()
    {
        var h = Build();
        var (saveData, _) = MakeSaveDataWithAccount("Alice", "hunter2", h.AccountService);
        h.Store.Seed(saveData);

        h.Handler.HandleNewConnection(h.Connection, h.GmcpHandler);
        await WaitUntilAsync(() => HasPhase(h.GmcpHandler, "name"));
        h.GmcpHandler.Sent.Clear();

        h.Connection.SimulateInput("Alice");
        await WaitUntilAsync(() => HasPhase(h.GmcpHandler, "password"));

        var phases = h.GmcpHandler.Sent.Where(x => x.Package == "Char.Login.Phase").ToList();
        phases.Should().ContainSingle().Which.Payload.Should().BeEquivalentTo(new { phase = "password" });
    }

    [Fact]
    public async Task ExistingPlayerLogin_SendsPlayingPhaseAfterSuccessfulLogin()
    {
        var h = Build();
        var (saveData, _) = MakeSaveDataWithAccount("Alice", "hunter2", h.AccountService);
        h.Store.Seed(saveData);

        h.Handler.HandleNewConnection(h.Connection, h.GmcpHandler);
        await WaitUntilAsync(() => HasPhase(h.GmcpHandler, "name"));
        h.Connection.SimulateInput("Alice");
        await WaitUntilAsync(() => h.Connection.SentText.Any(t => t.Contains("Password:")));
        await Task.Delay(100);
        h.GmcpHandler.Sent.Clear();

        h.Connection.SimulateInput("hunter2");
        await WaitUntilAsync(() => HasPhase(h.GmcpHandler, "playing"), 5000);

        var playingPhases = h.GmcpHandler.Sent
            .Where(x => x.Package == "Char.Login.Phase")
            .ToList();
        playingPhases.Should().ContainSingle()
            .Which.Payload.Should().BeEquivalentTo(new { phase = "playing" });

        // playing phase must arrive before world data
        var playingIdx = h.GmcpHandler.Sent.IndexOf(
            h.GmcpHandler.Sent.First(x => x.Package == "Char.Login.Phase"));
        var firstWorldIdx = h.GmcpHandler.Sent.FindIndex(
            x => x.Package != "Char.Login.Phase");
        if (firstWorldIdx >= 0)
        {
            playingIdx.Should().BeLessThan(firstWorldIdx);
        }
    }

    // ---- Tests: new player creation confirmation ----

    [Fact]
    public async Task NewPlayerCreation_SendsPasswordPhaseOnCreation()
    {
        var h = Build(); // empty store = new player
        h.Handler.HandleNewConnection(h.Connection, h.GmcpHandler);
        await WaitUntilAsync(() => HasPhase(h.GmcpHandler, "name"));
        h.GmcpHandler.Sent.Clear();

        h.Connection.SimulateInput("Newguy");
        await WaitUntilAsync(() => HasPhase(h.GmcpHandler, "email"));

        h.Connection.SimulateInput("newguy@test.com");
        await WaitUntilAsync(() => HasPhase(h.GmcpHandler, "password"));

        var phases = h.GmcpHandler.Sent.Where(x => x.Package == "Char.Login.Phase").ToList();
        phases.Should().Contain(x => x.Payload.GetType().GetProperty("phase")!
                                      .GetValue(x.Payload)!.ToString() == "password");
    }

    [Fact]
    public async Task NewPlayerCreation_PasswordMismatch_RepromptsAndCountsAttempt()
    {
        var h = Build();
        h.Handler.HandleNewConnection(h.Connection, h.GmcpHandler);
        await WaitUntilAsync(() => HasPhase(h.GmcpHandler, "name"));
        h.Connection.SimulateInput("Newguy");
        await WaitUntilAsync(() => h.Connection.SentText.Any(t => t.Contains("Email")));
        h.Connection.SimulateInput("newguy@test.com");
        await WaitUntilAsync(() => h.Connection.SentText.Any(t => t.Contains("password")));
        h.Connection.SimulateInput("goodpassword");  // first password
        await WaitUntilAsync(() => h.Connection.SentText.Any(t => t.Contains("Confirm")));

        h.Connection.SentText.Clear();
        h.Connection.SimulateInput("differentpassword");  // confirm - mismatch
        await WaitUntilAsync(() => h.Connection.SentText.Any(t => t.Contains("don't match")));

        h.Connection.SentText.Should().Contain(t => t.Contains("don't match"));
        h.Connection.IsConnected.Should().BeTrue();  // not yet disconnected
    }

    [Fact]
    public async Task NewPlayerCreation_ThreeFailures_Disconnects()
    {
        var h = Build();
        h.Handler.HandleNewConnection(h.Connection, h.GmcpHandler);
        await WaitUntilAsync(() => HasPhase(h.GmcpHandler, "name"));
        h.Connection.SimulateInput("Newguy");
        await WaitUntilAsync(() => h.Connection.SentText.Any(t => t.Contains("Email")));
        h.Connection.SimulateInput("newguy@test.com");
        await WaitUntilAsync(() => h.Connection.SentText.Any(t => t.Contains("password")));

        // fail 1: too short -- wait for re-prompt so flow is back at ReadLineAsync
        h.Connection.SimulateInput("ab");
        await WaitUntilAsync(() => h.Connection.SentText.Count(t => t.Contains("Choose a password")) >= 2);
        // fail 2: enter valid length, then mismatch on confirm
        h.Connection.SimulateInput("goodpassword");
        await WaitUntilAsync(() => h.Connection.SentText.Any(t => t.Contains("Confirm")));
        h.Connection.SimulateInput("wrongconfirm");
        await WaitUntilAsync(() => h.Connection.SentText.Count(t => t.Contains("Choose a password")) >= 3);
        // fail 3: too short again
        h.Connection.SimulateInput("ab");
        await WaitUntilAsync(() => !h.Connection.IsConnected);

        h.Connection.IsConnected.Should().BeFalse();
        h.Connection.SentText.Should().Contain(t => t.Contains("Too many"));
    }

    [Fact]
    public async Task NewPlayerCreation_MatchingPasswords_SendsCreatingPhase()
    {
        var h = Build();
        h.Handler.HandleNewConnection(h.Connection, h.GmcpHandler);
        await WaitUntilAsync(() => HasPhase(h.GmcpHandler, "name"));
        h.Connection.SimulateInput("Newguy");
        await WaitUntilAsync(() => h.Connection.SentText.Any(t => t.Contains("Email")));
        h.Connection.SimulateInput("newguy@test.com");
        await WaitUntilAsync(() => h.Connection.SentText.Any(t => t.Contains("password")));

        h.GmcpHandler.Sent.Clear();
        h.Connection.SimulateInput("goodpassword");   // first password
        await WaitUntilAsync(() => h.Connection.SentText.Any(t => t.Contains("Confirm")));
        h.Connection.SimulateInput("goodpassword");   // confirm
        await WaitUntilAsync(() => HasPhase(h.GmcpHandler, "creating"), 3000);

        var creatingPhases = h.GmcpHandler.Sent
            .Where(x => x.Package == "Char.Login.Phase")
            .ToList();
        creatingPhases.Should().Contain(x => x.Payload.Should().BeEquivalentTo(new { phase = "creating" }) != null);
    }
}
