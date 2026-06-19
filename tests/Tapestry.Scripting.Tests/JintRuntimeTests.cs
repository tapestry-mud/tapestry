using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Quests;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Races;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Help;
using Tapestry.Engine.Registration;
using Tapestry.Engine.Stats;
using Tapestry.Engine.Tags;
using Tapestry.Scripting;
using Tapestry.Scripting.Modules;
using Tapestry.Scripting.Services;
using Tapestry.Shared;

namespace Tapestry.Scripting.Tests;

public class JintRuntimeTests
{
    [Fact]
    public void ExecuteScript_RegistersCommand()
    {
        var (runtime, ctx) = CreateRuntime();
        var script = """
            tapestry.commands.register({
                name: 'ping',
                aliases: ['p'],
                priority: 0,
                handler: function(player, args) {
                    tapestry.world.send(player.entityId, 'Pong!\r\n');
                }
            });
            """;

        EsmTest.Load(runtime, "test-pack", script);
        ctx.RegistrationPolicy.Resolve();

        ctx.CommandRegistry.Resolve("ping").Should().NotBeNull();
        ctx.CommandRegistry.Resolve("p").Should().NotBeNull();
    }

    [Fact]
    public void ExecuteScript_RegistersEmote()
    {
        var (runtime, ctx) = CreateRuntime();
        var script = """
            tapestry.emotes.register({
                name: 'smile',
                self: 'You smile warmly.',
                room: '{name} smiles warmly.'
            });
            """;

        EsmTest.Load(runtime, "test-pack", script);
        ctx.RegistrationPolicy.Resolve(); // emotes commit at the seal barrier

        ctx.EmoteRegistry.Get("smile").Should().NotBeNull();
    }

    [Fact]
    public void ExecuteScript_SubscribesToEvents()
    {
        var (runtime, ctx) = CreateRuntime();

        // Set up a player session to capture sent text
        var connection = new FakeConnection();
        var entity = new Entity("player", "TestPlayer");
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.Hp = 100;
        var session = new PlayerSession(connection, entity);
        ctx.Sessions.Add(session);
        ctx.World.TrackEntity(entity);

        var script = """
            tapestry.events.on('test.event', function(event) {
                tapestry.world.send(event.sourceEntityId, 'Event received!\r\n');
            });
            """;

        EsmTest.Load(runtime, "test-pack", script);

        ctx.EventBus.Publish(new GameEvent
        {
            Type = "test.event",
            SourceEntityId = entity.Id
        });

        string.Join("", connection.SentText).Should().Contain("Event received!");
    }

    [Fact]
    public void ExecuteScript_InvokesCommandHandler()
    {
        var (runtime, ctx) = CreateRuntime();

        // Set up a player session to capture sent text
        var connection = new FakeConnection();
        var entity = new Entity("player", "TestPlayer");
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.Hp = 100;
        var session = new PlayerSession(connection, entity);
        ctx.Sessions.Add(session);
        ctx.World.TrackEntity(entity);

        var room = new Room("test", "Test", "Test room");
        room.AddEntity(entity);
        ctx.World.AddRoom(room);

        var script = """
            tapestry.commands.register({
                name: 'ping',
                handler: function(player, args) {
                    tapestry.world.send(player.entityId, 'Pong!\r\n');
                }
            });
            """;

        EsmTest.Load(runtime, "test-pack", script);
        ctx.RegistrationPolicy.Resolve();

        var registration = ctx.CommandRegistry.Resolve("ping");
        registration.Should().NotBeNull();

        var cmdCtx = new ActorContext
        {
            EntityId = entity.Id,
            RawInput = "ping",
            Command = "ping",
            RawArgs = []
        };

        var act = () => registration!.ActorHandler(cmdCtx);
        act.Should().NotThrow();
        string.Join("", connection.SentText).Should().Contain("Pong!");
    }

    [Fact]
    public void ExecuteScript_TimeLimitEnforced()
    {
        var (runtime, _) = CreateRuntime();
        var script = "while(true) {}";

        var act = () => EsmTest.Load(runtime, "test-pack", script);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ExecuteScript_MemoryLimitEnforced()
    {
        var (runtime, _) = CreateRuntime();
        // Allocate a large array in a loop to exhaust memory
        var script = "var arr = []; while(true) { arr.push(new Array(10000).fill(1)); }";
        var act = () => EsmTest.Load(runtime, "test-pack", script);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Stats_AccessibleFromScript()
    {
        var (runtime, ctx) = CreateRuntime();

        var connection = new FakeConnection();
        var entity = new Entity("player", "TestPlayer");
        entity.Stats.BaseStrength = 20;
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.Hp = 100;
        var session = new PlayerSession(connection, entity);
        ctx.Sessions.Add(session);
        ctx.World.TrackEntity(entity);

        var room = new Room("test", "Test", "Test room");
        room.AddEntity(entity);
        ctx.World.AddRoom(room);

        EsmTest.Load(runtime, "test", """
            tapestry.commands.register({
                name: 'teststats',
                handler: function(player, args) {
                    player.send('STR:' + player.stats.strength + ' HP:' + player.stats.hp + '/' + player.stats.maxHp);
                }
            });
            """);
        ctx.RegistrationPolicy.Resolve();

        var cmdCtx = new ActorContext
        {
            EntityId = entity.Id,
            RawInput = "teststats",
            Command = "teststats",
            RawArgs = Array.Empty<string>()
        };

        ctx.CommandRegistry.Resolve("teststats")!.ActorHandler(cmdCtx);
        string.Join("", connection.SentText).Should().Contain("STR:20 HP:100/100");
    }

    [Fact]
    public void MarkFileExecuted_IsIdempotent_AndPathNormalized()
    {
        var (runtime, _) = CreateRuntime();
        var path = Path.Combine(Path.GetTempPath(), "pack", "scripts", "quests", "q.js");

        runtime.HasExecutedFile(path).Should().BeFalse();

        // First mark records the file (caller should execute it)...
        runtime.MarkFileExecuted(path).Should().BeTrue();
        runtime.HasExecutedFile(path).Should().BeTrue();

        // ...and a non-normalised but equivalent path resolves to the same key, so the second
        // boot subsystem (QuestStartupModule) recognises the glob already ran it.
        var equivalent = Path.Combine(Path.GetTempPath(), "pack", "scripts", "..", "scripts", "quests", "q.js");
        runtime.HasExecutedFile(equivalent).Should().BeTrue();
        runtime.MarkFileExecuted(equivalent).Should().BeFalse();
    }

    internal static (JintRuntime, TestContext) CreateRuntime()
    {
        var commandRegistry = new CommandRegistry();
        var emoteRegistry = new EmoteRegistry();
        var eventBus = new EventBus();
        var world = new World();
        var sessions = new SessionManager();
        var alignmentConfig = new AlignmentConfig();
        var alignmentManagerForAI = new AlignmentManager(world, eventBus, alignmentConfig);
        var dispositionEvaluator = new DispositionEvaluator(world, eventBus, alignmentManagerForAI);
        var statDisplayNames = new StatDisplayNames();
        var slotRegistry = new SlotRegistry();
        var equipmentManager = new EquipmentManager(slotRegistry, eventBus);
        var currencyService = new CurrencyService(world, eventBus);
        var inventoryManager = new InventoryManager(eventBus, world, currencyService);
        var itemRegistry = new ItemRegistry();
        var combatManager = new CombatManager(world, eventBus);
        var mobAIManager = new MobAIManager(world, eventBus, combatManager, dispositionEvaluator, Microsoft.Extensions.Logging.Abstractions.NullLogger<Tapestry.Engine.Mobs.MobAIManager>.Instance, new TapestryMetrics());
        var effectManager = new EffectManager(world, eventBus);
        var progressionManager = new ProgressionManager(world, eventBus);
        var commandRouter = new CommandRouter(commandRegistry, sessions, world);
        var edges = new FakeEdges();
        var registrationPolicy = new Tapestry.Engine.Registration.RegistrationPolicy(edges);
        var gameLoop = new GameLoop(
            commandRouter,
            sessions, eventBus, new SystemEventQueue(),
            NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10), new NotificationQueue(),
            registrationPolicy);

        // Create service classes
        var questMarkerService = new QuestMarkerService(new QuestStateRepository(), new QuestRegistry());
        var messaging = new ApiMessaging(world, sessions, new NullGmcpModuleAdapter(), new CommandResponseContext(), new VisibilityFilter(), questMarkerService);
        var alignmentManager = new AlignmentManager(world, eventBus, new AlignmentConfig());
        var doorService = new DoorService(world, eventBus);
        var spawnManager = new SpawnManager(world, eventBus, new LootTableResolver(), itemRegistry);
        var worldOps = new ApiWorld(world, eventBus, sessions, mobAIManager, alignmentManager, messaging, doorService, new VisibilityFilter(), spawnManager, itemRegistry);
        var stats = new ApiStats(world, statDisplayNames);
        var mobs = new ApiMobs(world, mobAIManager, spawnManager);
        var transfer = new ApiTransfer(world, inventoryManager, equipmentManager);
        var mobCommandRegistry = new MobCommandRegistry(world, eventBus, NullLogger<MobCommandRegistry>.Instance);
        var tickTimer = new TickTimer(10);
        var mobCommandQueue = new MobCommandQueue(world, commandRouter, tickTimer, NullLogger<MobCommandQueue>.Instance);

        // Create modules
        var modules = new IJintApiModule[]
        {
            new CommandsModule(commandRegistry, messaging, worldOps, stats, world, NullLogger<CommandsModule>.Instance, new CommandResponseContext(), eventBus, new ArgResolver(world, new VisibilityFilter(), doorService, NullLogger<ArgResolver>.Instance), registrationPolicy, new HelpService()),
            new EmotesModule(emoteRegistry, registrationPolicy),
            new EventsModule(eventBus),
            new WorldModule(messaging, worldOps, world, gameLoop, new ClassRegistry(), new RaceRegistry(), mobAIManager, new NullGmcpModuleAdapter(), new TagRegistry(), new Tapestry.Engine.Persistence.PropertyRegistry(), new Tapestry.Engine.Mapping.AreaMapProjector(world, new Tapestry.Engine.Tags.TagRegistry()), new Tapestry.Engine.Mapping.AsciiMapRenderer()),
            new StatsModule(stats, statDisplayNames, world),
            new InventoryModule(inventoryManager, world, eventBus, messaging, transfer, slotRegistry),
            new EquipmentModule(equipmentManager, slotRegistry, world, transfer, registrationPolicy),
            new ItemsModule(itemRegistry, world),
            new CombatModule(combatManager, world, eventBus, gameLoop, effectManager),
            new ProgressionModule(progressionManager, registrationPolicy, NullLogger<ProgressionModule>.Instance),
            new MobsModule(mobs, mobAIManager, mobCommandRegistry, mobCommandQueue, commandRegistry, registrationPolicy, new MobInvocationBudget(), new ServerConfig(), NullLogger<MobsModule>.Instance),
            new Tapestry.Scripting.Modules.ThemeModule(new Tapestry.Engine.Color.ThemeRegistry(), registrationPolicy),
        };

        var loader = new Tapestry.Scripting.Interop.TapestryModuleLoader(edges);

        var runtime = new JintRuntime(modules, NullLogger<JintRuntime>.Instance, loader: loader);

        return (runtime, new TestContext
        {
            CommandRegistry = commandRegistry,
            EmoteRegistry = emoteRegistry,
            EventBus = eventBus,
            World = world,
            Sessions = sessions,
            Messaging = messaging,
            MobCommandQueue = mobCommandQueue,
            RegistrationPolicy = registrationPolicy,
            Runtime = runtime,
            Loader = loader,
            Edges = edges
        });
    }

    [Fact]
    public void RegisterMobCommand_RegistersVerb_AndDispatchQueuesFire()
    {
        var (runtime, ctx) = CreateRuntime();

        // Register a mob command via JS
        EsmTest.Load(runtime, "test-pack", """
            tapestry.mobs.registerCommand("say", {
                gmcp: { channel: "say" },
                handler: function(mob, text) {
                    tapestry.world.sendToRoom(mob.roomId, "handled:" + text);
                }
            });
            """);
        ctx.RegistrationPolicy.Resolve(); // mob commands commit at the seal barrier

        // Spawn a mob and place it in a room
        var room = new Room("test:room", "Town", "A room.");
        ctx.World.AddRoom(room);
        var mob = new Entity("npc", "Guard");
        mob.LocationRoomId = "test:room";
        ctx.World.TrackEntity(mob);

        // Connect a fake player to capture send output
        var connection = new FakeConnection();
        var player = new Entity("player", "Alice");
        player.LocationRoomId = "test:room";
        var session = new PlayerSession(connection, player);
        ctx.Sessions.Add(session);
        ctx.World.TrackEntity(player);

        // Queue a mob command (delay 0 = immediate)
        EsmTest.Load(runtime, "test-pack", $"tapestry.mobs.command('{mob.Id}', 'say Hello!', 0);");

        // ProcessTick to dispatch queued commands
        ctx.MobCommandQueue.ProcessTick();

        string.Join("", connection.SentText).Should().Contain("handled:Hello!");
    }

    [Fact]
    public void MobCommand_WithNoDelayArg_DoesNotThrow_AndQueuesImmediate()
    {
        var (runtime, ctx) = CreateRuntime();

        EsmTest.Load(runtime, "test-pack", """
            tapestry.mobs.registerCommand("say", {
                gmcp: { channel: "say" },
                handler: function(mob, text) {
                    tapestry.world.sendToRoom(mob.roomId, "handled:" + text);
                }
            });
            """);
        ctx.RegistrationPolicy.Resolve(); // mob commands commit at the seal barrier

        var room = new Room("test:room", "Town", "A room.");
        ctx.World.AddRoom(room);
        var mob = new Entity("npc", "Guard");
        mob.LocationRoomId = "test:room";
        ctx.World.TrackEntity(mob);

        var connection = new FakeConnection();
        var player = new Entity("player", "Alice");
        player.LocationRoomId = "test:room";
        var session = new PlayerSession(connection, player);
        ctx.Sessions.Add(session);
        ctx.World.TrackEntity(player);

        // Call with only 2 args — no delay argument — must not throw
        EsmTest.Load(runtime, "test-pack", $"tapestry.mobs.command('{mob.Id}', 'say Hello!');");

        ctx.MobCommandQueue.ProcessTick();

        string.Join("", connection.SentText).Should().Contain("handled:Hello!");
    }

    internal class TestContext
    {
        public required CommandRegistry CommandRegistry { get; init; }
        public required EmoteRegistry EmoteRegistry { get; init; }
        public required EventBus EventBus { get; init; }
        public required World World { get; init; }
        public required SessionManager Sessions { get; init; }
        public required ApiMessaging Messaging { get; init; }
        public required MobCommandQueue MobCommandQueue { get; init; }
        public required RegistrationPolicy RegistrationPolicy { get; init; }
        public JintRuntime Runtime { get; init; } = null!;
        public Tapestry.Scripting.Interop.TapestryModuleLoader Loader { get; init; } = null!;
        public FakeEdges Edges { get; init; } = null!;
        private readonly Dictionary<string, string> _packDirs = new();
        private readonly HashSet<(string from, string to)> _optionalEdges = new();

        // Creates a temp pack dir with dist/scripts, accumulates it into the loader's pack-dir map, rebuilds.
        public string RegisterTempPack(string ns)
        {
            var dir = System.IO.Directory.CreateTempSubdirectory("tap-esm-").FullName;
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "dist", "scripts"));
            _packDirs[ns] = dir;
            Loader.Build(_packDirs, _optionalEdges);
            return dir;
        }

        public void DeclareEdge(string from, string to) => Edges.Edges.Add((from, to));

        public Jint.Native.JsValue Evaluate(string js) => Runtime.EvaluateRaw(js);
    }
}
