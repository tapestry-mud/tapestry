using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Races;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Stats;
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

        runtime.Execute(script, "test-pack");

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

        runtime.Execute(script, "test-pack");

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

        runtime.Execute(script, "test-pack");

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

        runtime.Execute(script, "test-pack");

        var registration = ctx.CommandRegistry.Resolve("ping");
        registration.Should().NotBeNull();

        var cmdCtx = new CommandContext
        {
            PlayerEntityId = entity.Id,
            RawInput = "ping",
            Command = "ping",
            Args = []
        };

        var act = () => registration!.Handler(cmdCtx);
        act.Should().NotThrow();
        string.Join("", connection.SentText).Should().Contain("Pong!");
    }

    [Fact]
    public void ExecuteScript_TimeLimitEnforced()
    {
        var (runtime, _) = CreateRuntime();
        var script = "while(true) {}";

        var act = () => runtime.Execute(script, "test-pack");

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

        runtime.Execute(@"
            tapestry.commands.register({
                name: 'teststats',
                handler: function(player, args) {
                    player.send('STR:' + player.stats.strength + ' HP:' + player.stats.hp + '/' + player.stats.maxHp);
                }
            });
        ", "test");

        var cmdCtx = new CommandContext
        {
            PlayerEntityId = entity.Id,
            RawInput = "teststats",
            Command = "teststats",
            Args = Array.Empty<string>()
        };

        ctx.CommandRegistry.Resolve("teststats")!.Handler(cmdCtx);
        string.Join("", connection.SentText).Should().Contain("STR:20 HP:100/100");
    }

    [Fact]
    public void Execute_WithSourceFile_SetsCurrentSourceGlobal()
    {
        var (runtime, _) = CreateRuntime();

        runtime.Execute("var captured = __currentSource;", "test-pack", "scripts/commands/test.js");
        var result = runtime.Evaluate("captured");

        Assert.Equal("scripts/commands/test.js", result);
        var pack = runtime.Evaluate("__currentPack");
        Assert.Equal("test-pack", pack);
    }

    private static (JintRuntime, TestContext) CreateRuntime()
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
        var mobAIManager = new MobAIManager(world, eventBus, combatManager, dispositionEvaluator, Microsoft.Extensions.Logging.Abstractions.NullLogger<Tapestry.Engine.Mobs.MobAIManager>.Instance);
        var effectManager = new EffectManager(world, eventBus);
        var progressionManager = new ProgressionManager(world, eventBus);
        var gameLoop = new GameLoop(
            new CommandRouter(commandRegistry, sessions),
            sessions, eventBus, new SystemEventQueue(),
            NullLogger<GameLoop>.Instance, new TapestryMetrics());

        // Create service classes
        var messaging = new ApiMessaging(world, sessions);
        var alignmentManager = new AlignmentManager(world, eventBus, new AlignmentConfig());
        var doorService = new DoorService(world, eventBus);
        var worldOps = new ApiWorld(world, eventBus, sessions, mobAIManager, alignmentManager, messaging, doorService);
        var stats = new ApiStats(world, statDisplayNames);
        var spawnManager = new SpawnManager(world, eventBus, new LootTableResolver(), itemRegistry);
        var mobs = new ApiMobs(world, mobAIManager, spawnManager);
        var transfer = new ApiTransfer(world, inventoryManager, equipmentManager);

        // Create modules
        var modules = new IJintApiModule[]
        {
            new CommandsModule(commandRegistry, messaging, worldOps, stats, world, NullLogger<CommandsModule>.Instance),
            new EmotesModule(emoteRegistry),
            new EventsModule(eventBus),
            new WorldModule(messaging, worldOps, world, gameLoop, new ClassRegistry(), new RaceRegistry(), mobAIManager),
            new StatsModule(stats, statDisplayNames, world),
            new InventoryModule(inventoryManager, world, eventBus, messaging, transfer, slotRegistry),
            new EquipmentModule(equipmentManager, slotRegistry, world, transfer),
            new ItemsModule(itemRegistry, world),
            new CombatModule(combatManager, world, eventBus, gameLoop, effectManager),
            new ProgressionModule(progressionManager, NullLogger<ProgressionModule>.Instance),
            new MobsModule(mobs, mobAIManager, NullLogger<MobsModule>.Instance),
            new Tapestry.Scripting.Modules.ThemeModule(new Tapestry.Engine.Color.ThemeRegistry()),
        };

        var runtime = new JintRuntime(modules, NullLogger<JintRuntime>.Instance);

        return (runtime, new TestContext
        {
            CommandRegistry = commandRegistry,
            EmoteRegistry = emoteRegistry,
            EventBus = eventBus,
            World = world,
            Sessions = sessions,
            Messaging = messaging
        });
    }

    private class TestContext
    {
        public required CommandRegistry CommandRegistry { get; init; }
        public required EmoteRegistry EmoteRegistry { get; init; }
        public required EventBus EventBus { get; init; }
        public required World World { get; init; }
        public required SessionManager Sessions { get; init; }
        public required ApiMessaging Messaging { get; init; }
    }
}
