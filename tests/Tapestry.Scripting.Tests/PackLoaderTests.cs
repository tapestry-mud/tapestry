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
using Tapestry.Engine.Tags;
using Tapestry.Engine.Training;
using Tapestry.Engine.Ui;
using Tapestry.Engine.Quests;
using Tapestry.Scripting;
using Tapestry.Scripting.Interop;
using Tapestry.Scripting.Modules;
using Tapestry.Scripting.Services;

namespace Tapestry.Scripting.Tests;

public class PackLoaderTests
{
    private static string ExamplePackPath()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "tests", "fixtures", "packs", "example-pack")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        if (dir == null)
        {
            throw new DirectoryNotFoundException("Could not locate tests/fixtures/packs/example-pack from " + AppContext.BaseDirectory);
        }
        return Path.Combine(dir, "tests", "fixtures", "packs", "example-pack");
    }

    [Fact]
    public void LoadPack_ParsesManifestAndLoadsRooms()
    {
        var (world, _, _, loader) = CreateLoaderDepsWithSpawn();
        loader.Load(ExamplePackPath());

        world.GetRoom("tapestry-example-pack:test-arena").Should().NotBeNull();
        world.GetRoom("tapestry-example-pack:test-arena")!.Name.Should().Be("The Void");
        world.GetRoom("tapestry-example-pack:town-square").Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void LoadDeclarations_NullOrEmptyDirectory_Throws(string? packDirectory)
    {
        var (_, _, _, loader) = CreateLoaderDepsWithSpawn();

        var act = () => loader.LoadDeclarations(packDirectory!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LoadPack_RegistersItemTemplates()
    {
        var (_, itemRegistry, _, loader) = CreateLoaderDepsWithSpawn();
        loader.Load(ExamplePackPath());

        itemRegistry.HasTemplate("tapestry-example-pack:iron-sword").Should().BeTrue();
        var item = itemRegistry.CreateItem("tapestry-example-pack:iron-sword");
        item.Should().NotBeNull();
        item!.Name.Should().Be("an iron sword");
    }

    [Fact]
    public void LoadPack_CarriesContentsOntoItemTemplate()
    {
        var (_, itemRegistry, _, loader) = CreateLoaderDepsWithSpawn();
        loader.Load(ExamplePackPath());

        var template = itemRegistry.GetTemplate("tapestry-example-pack:oak-chest");
        template.Should().NotBeNull();
        template!.Contents.Should().HaveCount(1);
        template.Contents[0].TemplateId.Should().Be("tapestry-example-pack:iron-sword");
    }

    [Fact]
    public void LoadPack_RegistersMobTemplates()
    {
        var (_, _, spawnManager, loader) = CreateLoaderDepsWithSpawn();
        loader.Load(ExamplePackPath());

        spawnManager.GetTemplate("tapestry-example-pack:goblin").Should().NotBeNull();
        spawnManager.GetTemplate("tapestry-example-pack:test-dummy").Should().NotBeNull();
    }

    [Fact]
    public void LoadPack_RegistersInlineLootTable_ForGoblin()
    {
        var (_, _, spawnManager, loader) = CreateLoaderDepsWithSpawn();
        loader.Load(ExamplePackPath());

        var loot = spawnManager.GetLootTable("tapestry-example-pack:goblin");
        loot.Should().NotBeNull();
        loot!.Guaranteed.Should().NotBeEmpty();
        loot.Guaranteed[0].Item.Should().Be("tapestry-example-pack:goblin-ear");
    }

    [Fact]
    public void LoadPack_PlacesFixturesInRooms()
    {
        var (world, _, _, loader) = CreateLoaderDepsWithSpawn();
        loader.Load(ExamplePackPath());

        var townSquare = world.GetRoom("tapestry-example-pack:town-square");
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
        config!.Spawns.Should().Contain(s => s.Mob == "tapestry-example-pack:goblin" && s.Room == "tapestry-example-pack:training-grounds");
    }

    [Fact]
    public void LoadPack_PopulatesTagRegistry_WithOwnPackTags()
    {
        var tagRegistry = new TagRegistry();
        var (_, _, _, loader) = CreateLoaderDepsWithSpawn(tagRegistry);

        loader.Load(ExamplePackPath());

        // example-pack's own tags are in the registry (cursed, lair from tags.yml)
        tagRegistry.IsKnown("cursed", "tapestry-example-pack").Should().BeTrue();
        tagRegistry.IsKnown("lair", "tapestry-example-pack").Should().BeTrue();

        // Engine tags are NOT present -- tapestry-core was not loaded
        tagRegistry.IsKnown("no_kill", null).Should().BeFalse();
    }

    [Fact]
    public void LoadPack_ThrowsOnDuplicateId()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tapestry-dup-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(dir, "areas", "test", "items"));
        File.WriteAllText(Path.Combine(dir, "pack.yaml"), """
            name: test
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

    private static (World World, ItemRegistry ItemRegistry, SpawnManager SpawnManager, PackLoader Loader) CreateLoaderDepsWithSpawn(TagRegistry? tagRegistry = null)
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
        var commandRouter = new CommandRouter(commandRegistry, sessions, world);
        var gameLoop = new GameLoop(
            commandRouter,
            sessions, eventBus, new SystemEventQueue(),
            NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10), new NotificationQueue());
        var questMarkerService = new QuestMarkerService(new QuestStateRepository(), new QuestRegistry());
        var messaging = new ApiMessaging(world, sessions, new NullGmcpModuleAdapter(), new CommandResponseContext(), new VisibilityFilter(), questMarkerService);
        var alignmentManager = new AlignmentManager(world, eventBus, new AlignmentConfig());
        var doorService = new DoorService(world, eventBus);
        var worldOps = new ApiWorld(world, eventBus, sessions, mobAIManager, alignmentManager, messaging, doorService, new VisibilityFilter());
        var stats = new ApiStats(world, statDisplayNames);
        var mobsApi = new ApiMobs(world, mobAIManager, spawnManager);
        var mobCommandRegistry = new MobCommandRegistry(world, eventBus, NullLogger<MobCommandRegistry>.Instance);
        var tickTimer = new TickTimer(10);
        var mobCommandQueue = new MobCommandQueue(world, commandRouter, tickTimer, NullLogger<MobCommandQueue>.Instance);
        var transfer = new ApiTransfer(world, inventoryManager, equipmentManager);
        var classRegistry = new ClassRegistry();
        var raceRegistry = new RaceRegistry();
        var abilityRegistry = new AbilityRegistry();
        var proficiencyManager = new ProficiencyManager(world, abilityRegistry);
        var panelRenderer = new PanelRenderer(themeRegistry);
        var trainingConfig = new TrainingConfig();
        var trainingManager = new TrainingManager(world, proficiencyManager, raceRegistry, trainingConfig, abilityRegistry);
        var serverConfig = new ServerConfig();
        var areaRegistry = new AreaRegistry();
        var weatherZoneRegistry = new WeatherZoneRegistry();
        var areaTickService = new AreaTickService(world, eventBus, areaRegistry, serverConfig);
        var gameClock = new GameClock(eventBus, serverConfig);
        var weatherService = new WeatherService(areaRegistry, weatherZoneRegistry, world, sessions, eventBus, serverConfig);
        var returnAddressService = new ReturnAddressService(eventBus);
        var temporaryExitService = new TemporaryExitService(world, eventBus, areaTickService);
        var consumableService = new ConsumableService(world, eventBus);
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
            new CommandsModule(commandRegistry, messaging, worldOps, stats, world, NullLogger<CommandsModule>.Instance, new CommandResponseContext(), eventBus, new ArgResolver(world, new VisibilityFilter(), doorService, NullLogger<ArgResolver>.Instance)),
            new EmotesModule(emoteRegistry),
            new EventsModule(eventBus),
            new WorldModule(messaging, worldOps, world, gameLoop, classRegistry, raceRegistry, mobAIManager, new NullGmcpModuleAdapter(), new TagRegistry(), new Tapestry.Engine.Persistence.PropertyRegistry()),
            new StatsModule(stats, statDisplayNames, world),
            new InventoryModule(inventoryManager, world, eventBus, messaging, transfer, slotRegistry),
            new EquipmentModule(equipmentManager, slotRegistry, world, transfer),
            new ItemsModule(itemRegistry, world),
            new CombatModule(combatManager, world, eventBus, gameLoop, effectManager),
            new ProgressionModule(progressionManager, NullLogger<ProgressionModule>.Instance),
            new MobsModule(mobsApi, mobAIManager, mobCommandRegistry, mobCommandQueue, commandRegistry, NullLogger<MobsModule>.Instance),
            new ThemeModule(themeRegistry),
            new DiceModule(),
            new AbilitiesModule(abilityRegistry, proficiencyManager, world, gameLoop, eventBus, alignmentConfig),
            new EffectsModule(effectManager, world, abilityRegistry),
            new ClassesModule(classRegistry, raceRegistry, world, proficiencyManager, new NullGmcpModuleAdapter()),
            new RacesModule(raceRegistry, world),
            new AlignmentModule(alignmentManager, alignmentConfig, world),
            new UiModule(panelRenderer),
            new TrainingModule(trainingManager, proficiencyManager, trainingConfig),
            new AdminModule(world, messaging, sessions, panelRenderer, NullLogger<AdminModule>.Instance, new Tapestry.Engine.Persistence.PropertyRegistry(), new Tapestry.Engine.Tags.TagRegistry()),
            new CurrencyModule(world, currencyService),
            new ShopModule(world, shopService),
            new ConsumablesModule(consumableService),
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
        var helpService = new Tapestry.Engine.Help.HelpService();
        tagRegistry ??= new TagRegistry();
        var propertyRegistry = new Tapestry.Engine.Persistence.PropertyRegistry();
        var questRegistry = new QuestRegistry();
        var scheduleModule = new ScheduleModule(gameLoop, world);
        var loader = new PackLoader(world, slotRegistry, runtime, themeRegistry, spawnManager, itemRegistry,
            NullLogger<PackLoader>.Instance, packContext, areaRegistry, weatherZoneRegistry, helpService, tagRegistry,
            propertyRegistry, questRegistry, scheduleModule, new InteropCallSiteRegistry(),
            new LoadedPackNamespaces());

        return (world, itemRegistry, spawnManager, loader);
    }

    [Fact]
    public void LoadDeclarations_ExposesPackMotd_WhenManifestDeclaresMotdFile()
    {
        var packDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(packDir);

        try
        {
            var dataDir = Path.Combine(packDir, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "motd.txt"), "Hello from pack!");
            File.WriteAllText(Path.Combine(packDir, "tapestry.yaml"), """
                name: "test-motd-pack"
                version: "1.0.0"
                active: true
                load_order: 0
                content:
                  motd: "data/motd.txt"
                """);

            var (_, _, _, loader) = CreateLoaderDepsWithSpawn();
            loader.LoadDeclarations(packDir);

            loader.PackMotd.Should().Be("Hello from pack!");
        }
        finally
        {
            Directory.Delete(packDir, recursive: true);
        }
    }

    [Fact]
    public void LoadDeclarations_PackMotdIsNull_WhenNoMotdDeclared()
    {
        var (_, _, _, loader) = CreateLoaderDepsWithSpawn();
        loader.LoadDeclarations(ExamplePackPath());

        loader.PackMotd.Should().BeNull();
    }

    [Fact]
    public void LoadDeclarations_FirstPackWins_WhenMultiplePacksDeclareMotd()
    {
        var packDir1 = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var packDir2 = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(packDir1);
        Directory.CreateDirectory(packDir2);

        try
        {
            var dataDir1 = Path.Combine(packDir1, "data");
            Directory.CreateDirectory(dataDir1);
            File.WriteAllText(Path.Combine(dataDir1, "motd.txt"), "First pack wins!");
            File.WriteAllText(Path.Combine(packDir1, "tapestry.yaml"), """
                name: "test-motd-pack-1"
                version: "1.0.0"
                active: true
                load_order: 0
                content:
                  motd: "data/motd.txt"
                """);

            var dataDir2 = Path.Combine(packDir2, "data");
            Directory.CreateDirectory(dataDir2);
            File.WriteAllText(Path.Combine(dataDir2, "motd.txt"), "Second pack loses!");
            File.WriteAllText(Path.Combine(packDir2, "tapestry.yaml"), """
                name: "test-motd-pack-2"
                version: "1.0.0"
                active: true
                load_order: 10
                content:
                  motd: "data/motd.txt"
                """);

            var (_, _, _, loader) = CreateLoaderDepsWithSpawn();
            loader.LoadDeclarations(packDir1);
            loader.LoadDeclarations(packDir2);

            loader.PackMotd.Should().Be("First pack wins!");
        }
        finally
        {
            Directory.Delete(packDir1, recursive: true);
            Directory.Delete(packDir2, recursive: true);
        }
    }

    [Fact]
    public void LoadPack_RecordsInteropCallSites_FromScripts()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tapestry-interop-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var scriptsDir = Path.Combine(tempDir, "scripts");
            Directory.CreateDirectory(scriptsDir);

            File.WriteAllText(Path.Combine(tempDir, "tapestry.yaml"), """
                name: "@tapestry/scan-fixture"
                version: "1.0.0"
                active: true
                load_order: 0
                content:
                  scripts: "scripts/**/*.js"
                """);

            File.WriteAllText(Path.Combine(scriptsDir, "cold-path.js"), """
                function coldPath() {
                  tapestry.packs.call('@tapestry/foo', 'bar');
                }
                """);

            var registry = new InteropCallSiteRegistry();
            var loader = CreateLoaderWithRegistry(registry);

            loader.Load(tempDir);

            registry.All.Should().ContainSingle()
                .Which.Should().BeEquivalentTo(new
                {
                    CallerPack = "tapestry-scan-fixture",
                    TargetPack = "tapestry-foo",
                    ExportName = "bar",
                    Kind = InteropCallKind.Call,
                });
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static PackLoader CreateLoaderWithRegistry(InteropCallSiteRegistry registry)
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
        var commandRouter = new CommandRouter(commandRegistry, sessions, world);
        var gameLoop = new GameLoop(
            commandRouter,
            sessions, eventBus, new SystemEventQueue(),
            NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10), new NotificationQueue());
        var questMarkerService = new QuestMarkerService(new QuestStateRepository(), new QuestRegistry());
        var messaging = new ApiMessaging(world, sessions, new NullGmcpModuleAdapter(), new CommandResponseContext(), new VisibilityFilter(), questMarkerService);
        var alignmentManager = new AlignmentManager(world, eventBus, new AlignmentConfig());
        var doorService = new DoorService(world, eventBus);
        var worldOps = new ApiWorld(world, eventBus, sessions, mobAIManager, alignmentManager, messaging, doorService, new VisibilityFilter());
        var stats = new ApiStats(world, statDisplayNames);
        var mobsApi = new ApiMobs(world, mobAIManager, spawnManager);
        var mobCommandRegistry = new MobCommandRegistry(world, eventBus, NullLogger<MobCommandRegistry>.Instance);
        var tickTimer = new TickTimer(10);
        var mobCommandQueue = new MobCommandQueue(world, commandRouter, tickTimer, NullLogger<MobCommandQueue>.Instance);
        var transfer = new ApiTransfer(world, inventoryManager, equipmentManager);
        var classRegistry = new ClassRegistry();
        var raceRegistry = new RaceRegistry();
        var abilityRegistry = new AbilityRegistry();
        var proficiencyManager = new ProficiencyManager(world, abilityRegistry);
        var panelRenderer = new PanelRenderer(themeRegistry);
        var trainingConfig = new TrainingConfig();
        var trainingManager = new TrainingManager(world, proficiencyManager, raceRegistry, trainingConfig, abilityRegistry);
        var serverConfig = new ServerConfig();
        var areaRegistry = new AreaRegistry();
        var weatherZoneRegistry = new WeatherZoneRegistry();
        var areaTickService = new AreaTickService(world, eventBus, areaRegistry, serverConfig);
        var gameClock = new GameClock(eventBus, serverConfig);
        var weatherService = new WeatherService(areaRegistry, weatherZoneRegistry, world, sessions, eventBus, serverConfig);
        var returnAddressService = new ReturnAddressService(eventBus);
        var temporaryExitService = new TemporaryExitService(world, eventBus, areaTickService);
        var consumableService = new ConsumableService(world, eventBus);
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
            new CommandsModule(commandRegistry, messaging, worldOps, stats, world, NullLogger<CommandsModule>.Instance, new CommandResponseContext(), eventBus, new ArgResolver(world, new VisibilityFilter(), doorService, NullLogger<ArgResolver>.Instance)),
            new EmotesModule(emoteRegistry),
            new EventsModule(eventBus),
            new WorldModule(messaging, worldOps, world, gameLoop, classRegistry, raceRegistry, mobAIManager, new NullGmcpModuleAdapter(), new TagRegistry(), new Tapestry.Engine.Persistence.PropertyRegistry()),
            new StatsModule(stats, statDisplayNames, world),
            new InventoryModule(inventoryManager, world, eventBus, messaging, transfer, slotRegistry),
            new EquipmentModule(equipmentManager, slotRegistry, world, transfer),
            new ItemsModule(itemRegistry, world),
            new CombatModule(combatManager, world, eventBus, gameLoop, effectManager),
            new ProgressionModule(progressionManager, NullLogger<ProgressionModule>.Instance),
            new MobsModule(mobsApi, mobAIManager, mobCommandRegistry, mobCommandQueue, commandRegistry, NullLogger<MobsModule>.Instance),
            new ThemeModule(themeRegistry),
            new DiceModule(),
            new AbilitiesModule(abilityRegistry, proficiencyManager, world, gameLoop, eventBus, alignmentConfig),
            new EffectsModule(effectManager, world, abilityRegistry),
            new ClassesModule(classRegistry, raceRegistry, world, proficiencyManager, new NullGmcpModuleAdapter()),
            new RacesModule(raceRegistry, world),
            new AlignmentModule(alignmentManager, alignmentConfig, world),
            new UiModule(panelRenderer),
            new TrainingModule(trainingManager, proficiencyManager, trainingConfig),
            new AdminModule(world, messaging, sessions, panelRenderer, NullLogger<AdminModule>.Instance, new Tapestry.Engine.Persistence.PropertyRegistry(), new Tapestry.Engine.Tags.TagRegistry()),
            new CurrencyModule(world, currencyService),
            new ShopModule(world, shopService),
            new ConsumablesModule(consumableService),
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
        var helpService = new Tapestry.Engine.Help.HelpService();
        var tagRegistry = new TagRegistry();
        var propertyRegistry = new Tapestry.Engine.Persistence.PropertyRegistry();
        var questRegistry = new QuestRegistry();
        var scheduleModule = new ScheduleModule(gameLoop, world);

        return new PackLoader(world, slotRegistry, runtime, themeRegistry, spawnManager, itemRegistry,
            NullLogger<PackLoader>.Instance, packContext, areaRegistry, weatherZoneRegistry, helpService, tagRegistry,
            propertyRegistry, questRegistry, scheduleModule, registry,
            new LoadedPackNamespaces());
    }

    [Fact]
    public void RestorePlacements_SeedsAuthoredContainerContentsFromYaml()
    {
        var (world, _, spawnManager, loader) = CreateLoaderDepsWithSpawn();
        loader.Load(ExamplePackPath());

        // The oak-chest is an authored fixture in town-square; its template declares
        // contents: [iron-sword]. At boot the chest is placed bare (boot gap); the
        // first reset tick seeds its authored contents.
        var room = world.GetRoom("tapestry-example-pack:town-square")!;
        var chest = room.Entities.Single(e =>
            e.GetProperty<string>(CommonProperties.TemplateId) == "tapestry-example-pack:oak-chest");

        spawnManager.RestorePlacements("starter-town");

        chest.Contents.Count(e =>
            e.GetProperty<string>(CommonProperties.TemplateId) == "tapestry-example-pack:iron-sword")
            .Should().Be(1);
    }

    [Fact]
    public void LoadPack_RegistersRoomFixturesForRestore()
    {
        var (world, _, spawnManager, loader) = CreateLoaderDepsWithSpawn();
        loader.Load(ExamplePackPath());

        var room = world.GetRoom("tapestry-example-pack:town-square")!;
        var fountain = room.Entities.Single(e =>
            e.GetProperty<string>(CommonProperties.TemplateId) == "tapestry-example-pack:fountain");
        room.RemoveEntity(fountain);

        spawnManager.RestorePlacements("starter-town");

        room.Entities.Count(e =>
            e.GetProperty<string>(CommonProperties.TemplateId) == "tapestry-example-pack:fountain")
            .Should().Be(1);
    }

    private sealed class NullFlowPersistence : IFlowPersistence
    {
        public bool PlayerExists(string name) { return false; }
        public void SaveNewPlayer(Entity entity, Guid accountId) { }
    }
}
