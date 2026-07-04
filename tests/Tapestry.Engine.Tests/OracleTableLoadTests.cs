using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Data;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Color;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Help;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Quests;
using Tapestry.Engine.Registration;
using Tapestry.Engine.Stats;
using Tapestry.Engine.Tags;
using Tapestry.Scripting;
using Tapestry.Scripting.Modules;
using Tapestry.Scripting.Services;
using Xunit;

namespace Tapestry.Engine.Tests;

public class OracleTableLoadTests
{
    [Fact]
    public void LoadOracleTable_parses_envelope_and_weighted_entries()
    {
        var yaml = """
        oracle_table:
          kind: mobs
          entries:
            - { w: 60, id: angry-cook, name: "Angry Cook", desc: "Red-faced.", balance_ref: mob }
            - { w: 40, id: scullion,   name: "Scullion",   desc: "Sooty.",     balance_ref: mob }
        """;

        var table = YamlContentLoader.LoadOracleTable(yaml);

        Assert.Equal("mobs", table.Kind);
        Assert.Equal(2, table.Entries.Count);
        Assert.Equal(60, table.Entries[0].W);
        Assert.Equal("angry-cook", table.Entries[0].Id);
        Assert.Equal("Angry Cook", table.Entries[0].Name);
        Assert.Equal("mob", table.Entries[0].BalanceRef);
    }

    [Fact]
    public void LoadOracleTable_captures_unknown_entry_keys_in_extra()
    {
        var yaml = """
        oracle_table:
          kind: items
          entries:
            - { w: 70, id: ladle, name: "Ladle", desc: "Dented.", balance_ref: weapon, rarity: common, custom_field: rare-drop }
        """;

        var table = YamlContentLoader.LoadOracleTable(yaml);

        Assert.Equal("common", table.Entries[0].Rarity);
        Assert.Equal("rare-drop", table.Entries[0].Extra["custom_field"]);
    }

    [Fact]
    public void SerializeOracleTable_round_trips_entries()
    {
        var table = new Tapestry.Shared.OracleTable
        {
            Kind = "mobs",
            Entries = new()
            {
                new Tapestry.Shared.OracleEntry
                {
                    W = 60,
                    Id = "angry-cook",
                    Name = "Angry Cook",
                    BalanceRef = "mob",
                },
            },
        };

        var yaml = YamlContentLoader.SerializeOracleTable(table);
        var reloaded = YamlContentLoader.LoadOracleTable(yaml);

        Assert.Equal("mobs", reloaded.Kind);
        Assert.Single(reloaded.Entries);
        Assert.Equal("angry-cook", reloaded.Entries[0].Id);
        Assert.Equal(60, reloaded.Entries[0].W);
    }

    [Fact]
    public void Registry_registers_and_gets_by_id()
    {
        var reg = new OracleTableRegistry();
        var table = new Tapestry.Shared.OracleTable
        {
            Id = "castle:kitchen:mobs",
            Kind = "mobs",
            Entries = new(),
        };

        reg.Register(table);

        Assert.True(reg.Contains("castle:kitchen:mobs"));
        Assert.Equal("mobs", reg.Get("castle:kitchen:mobs")!.Kind);
    }

    [Fact]
    public void PackLoader_loads_oracle_tables_from_glob_and_keys_by_area_and_kind()
    {
        using var tmp = new TempPack();
        tmp.WriteManifest(content: "oracle: \"areas/**/*-oracle-table.yaml\"");
        tmp.WriteFile("areas/castle-kitchen/mobs/mob-oracle-table.yaml", """
        oracle_table:
          kind: mobs
          entries:
            - { w: 60, id: angry-cook, name: "Angry Cook", desc: "Red-faced.", balance_ref: mob }
        """);

        var (loader, registry) = tmp.BuildLoaderWithOracleRegistry();
        loader.LoadContent(tmp.Dir, tmp.Manifest);

        // Exact canonical id - no hedge. <area-folder>:<kind>.
        Assert.True(registry.Contains("castle-kitchen:mobs"));
        Assert.Equal("angry-cook", registry.Get("castle-kitchen:mobs")!.Entries[0].Id);
    }

    // ---------------------------------------------------------------------------
    // Test helpers
    // ---------------------------------------------------------------------------

    private sealed class TempPack : IDisposable
    {
        public string Dir { get; }
        public Tapestry.Shared.PackManifest Manifest { get; private set; } = new();

        public TempPack()
        {
            Dir = Path.Combine(Path.GetTempPath(), "tapestry-oracle-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(Dir);
        }

        public void WriteManifest(string content = "")
        {
            var yaml = $"""
                name: "test-oracle-pack"
                version: "1.0.0"
                active: true
                load_order: 0
                content:
                  {content}
                """;
            File.WriteAllText(Path.Combine(Dir, "pack.yaml"), yaml);
            Manifest = YamlContentLoader.LoadManifest(yaml);
            Manifest.PackDirectory = Dir;
        }

        public void WriteFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(Dir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        public (PackLoader Loader, OracleTableRegistry Registry) BuildLoaderWithOracleRegistry()
        {
            var world = new World();
            var eventBus = new EventBus();
            var sessions = new SessionManager();
            var commandRegistry = new CommandRegistry();
            var slotRegistry = new SlotRegistry();
            var currencyService = new CurrencyService(world, eventBus);
            var inventoryManager = new InventoryManager(eventBus, world, currencyService);
            var itemRegistry = new ItemRegistry();
            var equipmentManager = new EquipmentManager(slotRegistry, eventBus);
            var spawnManager = new SpawnManager(world, eventBus, new LootTableResolver(), itemRegistry);
            var themeRegistry = new ThemeRegistry();
            var alignmentConfig = new AlignmentConfig();
            var alignmentManager = new AlignmentManager(world, eventBus, alignmentConfig);
            var dispositionEvaluator = new DispositionEvaluator(world, eventBus, alignmentManager);
            var combatManager = new CombatManager(world, eventBus);
            var mobAIManager = new MobAIManager(world, eventBus, combatManager, dispositionEvaluator,
                NullLogger<MobAIManager>.Instance, new TapestryMetrics(), vitalsService: new VitalsService(eventBus));
            var statDisplayNames = new StatDisplayNames();
            var effectManager = new Engine.Effects.EffectManager(world, eventBus);
            var progressionManager = new Engine.Progression.ProgressionManager(world, eventBus);
            var commandRouter = new CommandRouter(commandRegistry, sessions, world);
            var registrationPolicy = new RegistrationPolicy(new NoEdgeOracle());
            var gameLoop = new GameLoop(
                commandRouter,
                sessions, eventBus, new SystemEventQueue(),
                NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10), new NotificationQueue(),
                registrationPolicy);
            var packContext = new PackContext();
            var tagRegistry = new TagRegistry();
            var propertyRegistry = new Tapestry.Engine.Persistence.PropertyRegistry();
            var areaRegistry = new AreaRegistry();
            var weatherZoneRegistry = new WeatherZoneRegistry();
            var helpService = new HelpService();
            var questRegistry = new QuestRegistry();
            var scheduleModule = new ScheduleModule(gameLoop, world);
            var oracleRegistry = new OracleTableRegistry();
            var runtime = new JintRuntime(Array.Empty<IJintApiModule>(), NullLogger<JintRuntime>.Instance);

            var loader = new PackLoader(world, slotRegistry, runtime, themeRegistry, spawnManager, itemRegistry,
                NullLogger<PackLoader>.Instance, packContext, areaRegistry, weatherZoneRegistry, helpService, tagRegistry,
                propertyRegistry, questRegistry, scheduleModule,
                new LoadedPackNamespaces(), registrationPolicy, oracleRegistry);

            return (loader, oracleRegistry);
        }

        public void Dispose()
        {
            if (Directory.Exists(Dir))
            {
                Directory.Delete(Dir, recursive: true);
            }
        }
    }

    private sealed class NoEdgeOracle : IPackEdgeOracle
    {
        public bool DeclaresEdge(string fromPack, string toPack) => false;
    }
}
