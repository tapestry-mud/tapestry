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
using Tapestry.Engine.Sustenance;
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
    }

    private class FakeFlowPersistence : IFlowPersistence
    {
        public bool PlayerExists(string name) => false;
        public void SaveNewPlayer(Entity entity, string passwordHash) { }
    }

    // ---- Harness ----

    private record Harness(
        ConnectionHandler Handler,
        FakeGmcpHandler GmcpHandler,
        FakeConnection Connection,
        GmcpService GmcpService,
        FakePlayerStore Store,
        GameLoop GameLoop);

    private static Harness Build(Action<FakePlayerStore>? seed = null)
    {
        var store = new FakePlayerStore();
        seed?.Invoke(store);

        var sessions = new SessionManager();
        var playerCreator = new PlayerCreator();
        var world = new World(playerCreator);
        var registry = new PropertyTypeRegistry();
        CommonProperties.Register(registry);
        var serializer = new PlayerSerializer(registry);
        var persistence = new PlayerPersistenceService(
            store, serializer, sessions, world,
            NullLogger<PlayerPersistenceService>.Instance);
        var eventBus = new EventBus();
        var alignmentConfig = new AlignmentConfig();
        var alignmentManager = new AlignmentManager(world, eventBus, alignmentConfig);
        var gmcpService = new GmcpService(
            sessions, world, eventBus,
            new GameClock(eventBus, new ServerConfig()),
            new WeatherService(new AreaRegistry(), new WeatherZoneRegistry(), world, sessions, eventBus, new ServerConfig()),
            new ProgressionManager(world, eventBus),
            alignmentManager,
            new SustenanceConfig(),
            new CommandRegistry(),
            new EffectManager(world, eventBus),
            new CombatManager(world, eventBus),
            new AbilityRegistry(),
            new ThemeRegistry(),
            new RarityRegistry(),
            new EssenceRegistry(),
            new SlotRegistry());
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
            new CommandRouter(new CommandRegistry(), sessions),
            sessions, new EventBus(), new SystemEventQueue(),
            NullLogger<GameLoop>.Instance,
            new TapestryMetrics(), new TickTimer(10));

        var mobAI = new MobAIManager(world, eventBus,
            new CombatManager(world, eventBus),
            new DispositionEvaluator(world, eventBus, new AlignmentManager(world, eventBus, alignmentConfig)),
            NullLogger<MobAIManager>.Instance);

        var handler = new ConnectionHandler(
            sessions,
            world,
            new SystemEventQueue(),
            new TapestryMetrics(),
            persistence,
            config,
            NullLogger<ConnectionHandler>.Instance,
            NullLogger<Tapestry.Server.Login.LoginFlow>.Instance,
            NullLogger<PlayerSpawner>.Instance,
            flowEngine,
            new ColorRenderer(new ThemeRegistry()),
            new LoginGateRegistry(),
            gmcpService,
            gameLoop,
            mobAI);

        var conn = new FakeConnection();
        var gmcpHandler = new FakeGmcpHandler();

        return new Harness(handler, gmcpHandler, conn, gmcpService, store, gameLoop);
    }

    private static PlayerSaveData MakeSaveData(string name, string password)
    {
        var registry = new PropertyTypeRegistry();
        CommonProperties.Register(registry);
        var serializer = new PlayerSerializer(registry);

        var entity = new Entity("player", name);
        entity.LocationRoomId = "core:town-square";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        return serializer.ToSaveData(entity, hash, new List<Entity>(), new List<(Entity, List<Entity>)>());
    }

    // ---- Tests: existing player login ----

    [Fact]
    public void ExistingPlayerLogin_SendsNamePhaseOnConnect()
    {
        var h = Build(s => s.Seed(MakeSaveData("Alice", "hunter2")));
        h.Handler.HandleNewConnection(h.Connection, h.GmcpHandler);
        Thread.Sleep(100);

        h.GmcpHandler.Sent.Should().Contain(x => x.Package == "Char.Login.Phase");
        var namePhase = h.GmcpHandler.Sent.First(x => x.Package == "Char.Login.Phase");
        namePhase.Payload.Should().BeEquivalentTo(new { phase = "name" });
    }

    [Fact]
    public void ExistingPlayerLogin_SendsPasswordPhaseBeforePasswordPrompt()
    {
        var h = Build(s => s.Seed(MakeSaveData("Alice", "hunter2")));
        h.Handler.HandleNewConnection(h.Connection, h.GmcpHandler);
        Thread.Sleep(100);
        h.GmcpHandler.Sent.Clear();

        h.Connection.SimulateInput("Alice");
        Thread.Sleep(100);

        var phases = h.GmcpHandler.Sent.Where(x => x.Package == "Char.Login.Phase").ToList();
        phases.Should().ContainSingle().Which.Payload.Should().BeEquivalentTo(new { phase = "password" });
    }

    [Fact]
    public void ExistingPlayerLogin_SendsPlayingPhaseAfterSuccessfulLogin()
    {
        var h = Build(s => s.Seed(MakeSaveData("Alice", "hunter2")));
        h.Handler.HandleNewConnection(h.Connection, h.GmcpHandler);
        Thread.Sleep(100);
        h.Connection.SimulateInput("Alice");
        Thread.Sleep(100);
        h.GmcpHandler.Sent.Clear();

        h.Connection.SimulateInput("hunter2");
        Thread.Sleep(800); // BCrypt.Verify takes 200-400ms

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
    public void NewPlayerCreation_SendsPasswordPhaseOnCreation()
    {
        var h = Build(); // empty store = new player
        h.Handler.HandleNewConnection(h.Connection, h.GmcpHandler);
        Thread.Sleep(100);
        h.GmcpHandler.Sent.Clear();

        h.Connection.SimulateInput("Newguy");
        Thread.Sleep(100);

        var phases = h.GmcpHandler.Sent.Where(x => x.Package == "Char.Login.Phase").ToList();
        phases.Should().Contain(x => x.Payload.GetType().GetProperty("phase")!
                                      .GetValue(x.Payload)!.ToString() == "password");
    }

    [Fact]
    public void NewPlayerCreation_PasswordMismatch_RepromptsAndCountsAttempt()
    {
        var h = Build();
        h.Handler.HandleNewConnection(h.Connection, h.GmcpHandler);
        Thread.Sleep(100);
        h.Connection.SimulateInput("Newguy");
        Thread.Sleep(100);
        h.Connection.SimulateInput("goodpassword");  // first password
        Thread.Sleep(100);

        h.Connection.SentText.Clear();
        h.Connection.SimulateInput("differentpassword");  // confirm - mismatch
        Thread.Sleep(100);

        h.Connection.SentText.Should().Contain(t => t.Contains("don't match"));
        h.Connection.IsConnected.Should().BeTrue();  // not yet disconnected
    }

    [Fact]
    public void NewPlayerCreation_ThreeFailures_Disconnects()
    {
        var h = Build();
        h.Handler.HandleNewConnection(h.Connection, h.GmcpHandler);
        Thread.Sleep(100);
        h.Connection.SimulateInput("Newguy");
        Thread.Sleep(100);

        // fail 1: too short
        h.Connection.SimulateInput("ab");
        Thread.Sleep(100);
        // fail 2: enter valid length, then mismatch on confirm
        h.Connection.SimulateInput("goodpassword");
        Thread.Sleep(100);
        h.Connection.SimulateInput("wrongconfirm");
        Thread.Sleep(100);
        // fail 3: too short again
        h.Connection.SimulateInput("ab");
        Thread.Sleep(100);

        h.Connection.IsConnected.Should().BeFalse();
        h.Connection.SentText.Should().Contain(t => t.Contains("Too many"));
    }

    [Fact]
    public void NewPlayerCreation_MatchingPasswords_SendsCreatingPhase()
    {
        var h = Build();
        h.Handler.HandleNewConnection(h.Connection, h.GmcpHandler);
        Thread.Sleep(100);
        h.Connection.SimulateInput("Newguy");
        Thread.Sleep(100);

        h.GmcpHandler.Sent.Clear();
        h.Connection.SimulateInput("goodpassword");   // first password
        Thread.Sleep(100);
        h.Connection.SimulateInput("goodpassword");   // confirm
        Thread.Sleep(800); // BCrypt.HashPassword takes 200-400ms

        var creatingPhases = h.GmcpHandler.Sent
            .Where(x => x.Package == "Char.Login.Phase")
            .ToList();
        creatingPhases.Should().Contain(x => x.Payload.Should().BeEquivalentTo(new { phase = "creating" }) != null);
    }
}
