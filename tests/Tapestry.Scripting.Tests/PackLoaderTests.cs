using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Races;
using Tapestry.Engine.Color;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Consumables;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Rest;
using Tapestry.Engine.Stats;
using Tapestry.Engine.Sustenance;
using Tapestry.Engine.Training;
using Tapestry.Engine.Ui;
using Tapestry.Scripting;
using Tapestry.Scripting.Modules;
using Tapestry.Scripting.Services;

namespace Tapestry.Scripting.Tests;

public class PackLoaderTests
{
    private static string ExamplePackPath()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "packs", "example-pack")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        if (dir == null)
        {
            throw new DirectoryNotFoundException("Could not locate packs/example-pack from " + AppContext.BaseDirectory);
        }
        return Path.Combine(dir, "packs", "example-pack");
    }

    [Fact]
    public void LoadPack_ParsesManifestAndLoadsRooms()
    {
        var (world, _, _, loader) = CreateLoaderDepsWithSpawn();
        loader.Load(ExamplePackPath());

        world.GetRoom("example-pack:test-arena").Should().NotBeNull();
        world.GetRoom("example-pack:test-arena")!.Name.Should().Be("The Void");
        world.GetRoom("example-pack:town-square").Should().NotBeNull();
    }

    [Fact]
    public void LoadPack_RegistersItemTemplates()
    {
        var (_, itemRegistry, _, loader) = CreateLoaderDepsWithSpawn();
        loader.Load(ExamplePackPath());

        itemRegistry.HasTemplate("example-pack:iron-sword").Should().BeTrue();
        var item = itemRegistry.CreateItem("example-pack:iron-sword");
        item.Should().NotBeNull();
        item!.Name.Should().Be("an iron sword");
    }

    [Fact]
    public void LoadPack_RegistersMobTemplates()
    {
        var (_, _, spawnManager, loader) = CreateLoaderDepsWithSpawn();
        loader.Load(ExamplePackPath());

        spawnManager.GetTemplate("example-pack:goblin").Should().NotBeNull();
        spawnManager.GetTemplate("example-pack:test-dummy").Should().NotBeNull();
    }

    [Fact]
    public void LoadPack_RegistersInlineLootTable_ForGoblin()
    {
        var (_, _, spawnManager, loader) = CreateLoaderDepsWithSpawn();
        loader.Load(ExamplePackPath());

        var loot = spawnManager.GetLootTable("example-pack:goblin");
        loot.Should().NotBeNull();
        loot!.Guaranteed.Should().NotBeEmpty();
        loot.Guaranteed[0].Item.Should().Be("example-pack:goblin-ear");
    }

    [Fact]
    public void LoadPack_PlacesFixturesInRooms()
    {
        var (world, _, _, loader) = CreateLoaderDepsWithSpawn();
        loader.Load(ExamplePackPath());

        var townSquare = world.GetRoom("example-pack:town-square");
        townSquare.Should().NotBeNull();
        townSquare!.Entities.Should().Contain(e => e.Name == "a stone fountain");
    }

    [Fact]
    public void LoadPack_RegistersRoomSpawns()
    {
        var (_, _, spawnManager, loader) = CreateLoaderDepsWithSpawn();
        loader.Load(ExamplePackPath());

        var config = spawnManager.GetAreaConfig("starter-town");
        config.Should().NotBeNull();
        config!.Spawns.Should().Contain(s => s.Mob == "example-pack:goblin" && s.Room == "example-pack:training-grounds");
    }

    [Fact]
    public void LoadPack_ThrowsOnDuplicateId()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tapestry-dup-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(dir, "areas", "test", "items"));
        File.WriteAllText(Path.Combine(dir, "pack.yaml"), """
            name: test-pack
            version: "1.0.0"
            display_name: "Test"
            description: ""
            author: "Test"
            load_order: 0
            content:
              area_definitions: "areas/**/area.yaml"
              items: "areas/**/items/*.yaml"
            """);
        File.WriteAllText(Path.Combine(dir, "areas", "test", "area.yaml"), """
            area:
              id: test
              name: Test
              reset_interval: 300
            """);
        File.WriteAllText(Path.Combine(dir, "areas", "test", "items", "sword.yaml"), """
            id: "test:sword"
            name: "a sword"
            type: "item"
            tags: []
            properties: {}
            modifiers: []
            """);
        File.WriteAllText(Path.Combine(dir, "areas", "test", "items", "sword-dupe.yaml"), """
            id: "test:sword"
            name: "also a sword"
            type: "item"
            tags: []
            properties: {}
            modifiers: []
            """);
        try
        {
            var (_, _, _, loader) = CreateLoaderDepsWithSpawn();
            var act = () => loader.Load(dir);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Duplicate*test:sword*");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static (World World, ItemRegistry ItemRegistry, SpawnManager SpawnManager, PackLoader Loader) CreateLoaderDepsWithSpawn()
    {
        var world = new World();
        var eventBus = new EventBus();
        var sessions = new SessionManager();
        var commandRegistry = new CommandRegistry();
        var emoteRegistry = new EmoteRegistry();
        var slotRegistry = new SlotRegistry();
        var equipmentManager = new EquipmentManager(slotRegistry, eventBus);
        var currencyService = new CurrencyService(world, eventBus);
        var inventoryManager = new InventoryManager(eventBus, world, currencyService);
        var itemRegistry = new ItemRegistry();
        var spawnManager = new SpawnManager(world, eventBus, new LootTableResolver(), itemRegistry);
        var themeRegistry = new ThemeRegistry();
        var alignmentConfig = new AlignmentConfig();
        var alignmentManagerForAI = new AlignmentManager(world, eventBus, alignmentConfig);
        var dispositionEvaluator = new DispositionEvaluator(world, eventBus, alignmentManagerForAI);
        var combatManager = new CombatManager(world, eventBus);
        var mobAIManager = new MobAIManager(world, eventBus, combatManager, dispositionEvaluator,
            NullLogger<MobAIManager>.Instance);
        var statDisplayNames = new StatDisplayNames();
        var effectManager = new EffectManager(world, eventBus);
        var progressionManager = new ProgressionManager(world, eventBus);
        var gameLoop = new GameLoop(
            new CommandRouter(commandRegistry, sessions),
            sessions, eventBus, new SystemEventQueue(),
            NullLogger<GameLoop>.Instance, new TapestryMetrics());
        var messaging = new ApiMessaging(world, sessions, new NullGmcpModuleAdapter(), new CommandResponseContext());
        var alignmentManager = new AlignmentManager(world, eventBus, new AlignmentConfig());
        var doorService = new DoorService(world, eventBus);
        var worldOps = new ApiWorld(world, eventBus, sessions, mobAIManager, alignmentManager, messaging, doorService);
        var stats = new ApiStats(world, statDisplayNames);
        var mobsApi = new ApiMobs(world, mobAIManager, spawnManager);
        var mobCommandRegistry = new MobCommandRegistry(world, eventBus, NullLogger<MobCommandRegistry>.Instance);
        var tickTimer = new TickTimer(10);
        var mobCommandQueue = new MobCommandQueue(world, mobCommandRegistry, tickTimer, NullLogger<MobCommandQueue>.Instance);
        var transfer = new ApiTransfer(world, inventoryManager, equipmentManager);
        var classRegistry = new ClassRegistry();
        var raceRegistry = new RaceRegistry();
        var abilityRegistry = new AbilityRegistry();
        var proficiencyManager = new ProficiencyManager(world, abilityRegistry);
        var panelRenderer = new PanelRenderer(themeRegistry);
        var trainingConfig = new TrainingConfig();
        var trainingManager = new TrainingManager(world, proficiencyManager, raceRegistry, trainingConfig);
        var serverConfig = new ServerConfig();
        var areaRegistry = new AreaRegistry();
        var weatherZoneRegistry = new WeatherZoneRegistry();
        var areaTickService = new AreaTickService(world, eventBus, areaRegistry, serverConfig);
        var gameClock = new GameClock(eventBus, serverConfig);
        var weatherService = new WeatherService(areaRegistry, weatherZoneRegistry, world, sessions, eventBus, serverConfig);
        var returnAddressService = new ReturnAddressService(eventBus);
        var temporaryExitService = new TemporaryExitService(world, eventBus, areaTickService);
        var consumableService = new ConsumableService(world, eventBus);
        var sustenanceConfig = new SustenanceConfig();
        var economyConfig = new EconomyConfig();
        var shopService = new ShopService(world, eventBus, currencyService, economyConfig, itemRegistry, equipmentManager);
        var restService = new RestService(world, eventBus, gameLoop);
        var stackingService = new StackingService();
        var rarityRegistry = new RarityRegistry();
        var essenceRegistry = new EssenceRegistry();
        var packContext = new PackContext();
        var flowRegistry = new FlowRegistry();
        var playerCreator = new PlayerCreator();
        var flowEngine = new FlowEngine(flowRegistry, sessions, world, new NullFlowPersistence(), panelRenderer,
            classRegistry, raceRegistry, alignmentManager, playerCreator, eventBus);

        var modules = new IJintApiModule[]
        {
            new CommandsModule(commandRegistry, messaging, worldOps, stats, world, NullLogger<CommandsModule>.Instance),
            new EmotesModule(emoteRegistry),
            new EventsModule(eventBus),
            new WorldModule(messaging, worldOps, world, gameLoop, classRegistry, raceRegistry, mobAIManager),
            new StatsModule(stats, statDisplayNames, world),
            new InventoryModule(inventoryManager, world, eventBus, messaging, transfer, slotRegistry),
            new EquipmentModule(equipmentManager, slotRegistry, world, transfer),
            new ItemsModule(itemRegistry, world),
            new CombatModule(combatManager, world, eventBus, gameLoop, effectManager),
            new ProgressionModule(progressionManager, NullLogger<ProgressionModule>.Instance),
            new MobsModule(mobsApi, mobAIManager, mobCommandRegistry, mobCommandQueue, NullLogger<MobsModule>.Instance),
            new ThemeModule(themeRegistry),
            new DiceModule(),
            new AbilitiesModule(abilityRegistry, proficiencyManager, world, gameLoop, eventBus, alignmentConfig),
            new EffectsModule(effectManager, world, abilityRegistry),
            new ClassesModule(classRegistry, raceRegistry, world, proficiencyManager),
            new RacesModule(raceRegistry, world),
            new AlignmentModule(alignmentManager, alignmentConfig, world),
            new UiModule(panelRenderer),
            new TrainingModule(trainingManager, proficiencyManager, trainingConfig),
            new AdminModule(world, messaging, sessions, panelRenderer, NullLogger<AdminModule>.Instance),
            new CurrencyModule(world, currencyService),
            new ShopModule(world, shopService),
            new ConsumablesModule(consumableService, world, sustenanceConfig),
            new RestModule(restService),
            new DoorsModule(world, doorService, eventBus),
            new PortalsModule(world, temporaryExitService),
            new AreaModule(areaTickService, areaRegistry),
            new TimeModule(gameClock, serverConfig),
            new WeatherModule(weatherService),
            new ReturnAddressModule(world, returnAddressService),
            new DataModule(packContext),
            new RarityModule(rarityRegistry, themeRegistry),
            new EssenceModule(essenceRegistry, themeRegistry),
            new StackingModule(stackingService, world),
            new FlowsModule(flowRegistry, flowEngine, sessions),
            new GmcpModule(new NullGmcpModuleAdapter()),
        };

        var runtime = new JintRuntime(modules, NullLogger<JintRuntime>.Instance);
        var loader = new PackLoader(world, slotRegistry, runtime, themeRegistry, spawnManager, itemRegistry,
            NullLogger<PackLoader>.Instance, packContext, areaRegistry, weatherZoneRegistry);

        return (world, itemRegistry, spawnManager, loader);
    }

    private sealed class NullFlowPersistence : IFlowPersistence
    {
        public bool PlayerExists(string name) { return false; }
        public void SaveNewPlayer(Entity entity, string passwordHash) { }
    }
}
