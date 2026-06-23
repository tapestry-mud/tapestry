using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Color;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Heartbeat;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Prompt;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Races;
using Tapestry.Engine.Stats;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Consumables;
using Tapestry.Engine.Rest;
using Tapestry.Engine.Distribution;
using Tapestry.Engine.Ui;
using Tapestry.Engine.Help;
using Tapestry.Engine.Quests;
using Tapestry.Engine.Watch;
using Tapestry.Data;

namespace Tapestry.Engine;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTapestryEngine(this IServiceCollection services)
    {
        // Config
        services.TryAddSingleton<ServerConfig>();

        // Persistence
        services.AddSingleton<PropertyRegistry>();
        services.AddSingleton<PlayerSerializer>();

        // Core
        services.AddSingleton<World>();
        services.AddSingleton<StubExitResolver>();
        services.AddSingleton<EventBus>();
        services.AddSingleton<CommandRegistry>();
        services.AddSingleton<EmoteRegistry>();
        services.AddSingleton<BadInputTracker>(sp =>
            new BadInputTracker(
                sp.GetRequiredService<ILogger<BadInputTracker>>(),
                sp.GetRequiredService<TapestryMetrics>().BadInputTotal));
        services.AddSingleton<CommandRouter>(sp => new CommandRouter(
            sp.GetRequiredService<CommandRegistry>(),
            sp.GetRequiredService<SessionManager>(),
            sp.GetRequiredService<World>(),
            sp.GetRequiredService<BadInputTracker>(),
            sp.GetRequiredService<SwellClockManager>()));
        services.AddSingleton<SessionManager>();
        // Runtime-only (resets on reboot, ROM behavior) -- see WizlockState docs.
        services.AddSingleton<Login.WizlockState>();
        services.AddSingleton<WatchRegistry>();
        services.AddSingleton<WatchSessionHub>();
        services.AddSingleton<IWatchRosterSource, WatchRosterSource>();
        services.AddSingleton<SystemEventQueue>();
        services.AddSingleton<TapestryMetrics>();
        services.AddSingleton<GameLoop>();
        services.AddSingleton<Tapestry.Engine.Registration.RegistrationGate>();
        services.AddSingleton<Tapestry.Engine.Registration.RegistrationPolicy>();
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<ServerConfig>();
            return new TickTimer((int)Math.Round(1000.0 / config.Server.TickRateMs));
        });

        // Stats
        services.AddSingleton<StatDisplayNames>();

        // Inventory
        services.AddSingleton<SlotRegistry>();
        services.AddSingleton<RarityRegistry>();
        services.AddSingleton<EssenceRegistry>();
        services.AddSingleton<StackingService>();
        services.AddSingleton<EquipmentManager>();
        services.AddSingleton<InventoryManager>();

        // Items
        services.AddSingleton<ItemRegistry>();
        services.AddSingleton<LootTableResolver>();

        // Distribution
        services.AddSingleton<DistributionService>();

        // Mobs
        services.AddSingleton<SpawnManager>(sp => new SpawnManager(
            sp.GetRequiredService<World>(),
            sp.GetRequiredService<EventBus>(),
            sp.GetRequiredService<LootTableResolver>(),
            sp.GetRequiredService<ItemRegistry>(),
            sp.GetRequiredService<ClassRegistry>(),
            sp.GetRequiredService<RaceRegistry>(),
            proficiencyManager: sp.GetRequiredService<ProficiencyManager>()));
        services.AddSingleton<DispositionEvaluator>();
        services.AddSingleton<MobAIManager>();
        services.AddSingleton<MobCommandRegistry>();
        services.AddSingleton<MobCommandQueue>();

        // Combat
        services.AddSingleton<CombatManager>();
        services.AddSingleton<WindowValidatorRegistry>();
        services.AddSingleton<SwellClockManager>();
        services.AddSingleton<SwellClockPulse>();

        // Progression
        services.AddSingleton<ProgressionManager>();

        // Abilities
        services.AddSingleton<AbilityRegistry>();
        services.AddSingleton<ProficiencyManager>();
        services.AddSingleton<AbilityCommandBridge>();
        services.AddSingleton<PassiveAbilityProcessor>();

        // Classes
        services.AddSingleton<ClassRegistry>();
        services.AddSingleton<ClassPathProcessor>();

        // Races
        services.AddSingleton<RaceRegistry>();

        // Alignment
        services.AddSingleton<AlignmentConfig>();
        services.AddSingleton<AlignmentManager>();

        // Training
        services.AddSingleton<Tapestry.Engine.Training.TrainingConfig>();
        services.AddSingleton<Tapestry.Engine.Training.TrainingManager>();

        // Economy
        services.AddSingleton<EconomyConfig>();
        services.AddSingleton<CurrencyService>();
        services.AddSingleton<ShopService>();

        // Consumables
        services.AddSingleton<ConsumableService>();

        // Rest
        services.AddSingleton<RestConfig>();
        services.AddSingleton<RestService>();

        // Arg resolution
        services.AddSingleton<VisibilityFilter>();
        services.AddSingleton<ArgResolver>();

        // Doors / Portals / Areas
        services.AddSingleton<DoorService>();
        services.AddSingleton<AreaRegistry>();
        services.AddSingleton<OracleTableRegistry>();
        services.AddSingleton<WeatherZoneRegistry>();
        services.AddSingleton<GameClock>();
        services.AddSingleton<WeatherService>();
        services.AddSingleton<AreaTickService>();
        services.AddSingleton<TemporaryExitService>();
        services.AddSingleton<ReturnAddressService>();

        // Flow
        services.AddSingleton<PlayerCreator>();
        services.AddSingleton<FlowRegistry>();
        services.AddSingleton<FlowEngine>();
        services.TryAddSingleton<IFlowPersistence, NullFlowPersistence>();

        // Effects
        services.AddSingleton<EffectManager>();

        // Heartbeat
        services.AddSingleton<HeartbeatManager>();
        services.AddSingleton<AbilityResolutionPhase>(sp => new AbilityResolutionPhase(
            sp.GetRequiredService<RaceRegistry>(),
            sp.GetRequiredService<AlignmentManager>()));
        services.AddSingleton<CombatPulse>(sp =>
        {
            var abilityPhase = sp.GetRequiredService<AbilityResolutionPhase>();
            return new CombatPulse(
                abilityPhase,
                new List<ICombatPhase>
                {
                    new ResolveAutoAttacksPhase(),
                    new ResolveStatusEffectsPhase(),
                    new CheckWimpyPhase()
                });
        });

        // Notifications
        services.AddSingleton<NotificationQueue>();

        // Quests
        services.AddSingleton<QuestConfig>();
        services.AddSingleton<QuestRegistry>();
        services.AddSingleton<QuestStateRepository>();

        // Quest reward service interfaces -- packs override these via their own registrations.
        // Wire real service implementations to quest reward interfaces.
        // TryAddSingleton fallbacks below are no-ops when running with the full engine.
        services.AddSingleton<IQuestProgressionService>(sp => sp.GetRequiredService<ProgressionManager>());
        services.AddSingleton<IQuestCurrencyService>(sp => sp.GetRequiredService<CurrencyService>());
        services.AddSingleton<IQuestProficiencyService>(sp => sp.GetRequiredService<ProficiencyManager>());
        services.AddSingleton<IQuestItemRegistry>(sp => sp.GetRequiredService<ItemRegistry>());
        services.AddSingleton<IQuestInventoryService>(sp => sp.GetRequiredService<InventoryManager>());

        // No-op fallbacks for standalone/test use (engine without server infrastructure).
        services.TryAddSingleton<IQuestProgressionService, NullQuestProgressionService>();
        services.TryAddSingleton<IQuestCurrencyService, NullQuestCurrencyService>();
        services.TryAddSingleton<IQuestProficiencyService, NullQuestProficiencyService>();
        services.TryAddSingleton<IQuestItemRegistry, NullQuestItemRegistry>();
        services.TryAddSingleton<IQuestInventoryService, NullQuestInventoryService>();
        services.AddSingleton<QuestRewardDispatcher>();
        services.AddSingleton<IQuestRewardDispatcher>(sp => sp.GetRequiredService<QuestRewardDispatcher>());

        // QuestPersistenceService requires ServerConfig; register NullQuestPersistence as fallback.
        // When a full server is configured, callers should register QuestPersistenceService and
        // override IQuestPersistence before building the container.
        services.TryAddSingleton<IQuestPersistence, NullQuestPersistence>();

        // IQuestScriptLoader is registered by Tapestry.Scripting; fall back to NullQuestScriptLoader.
        services.TryAddSingleton<IQuestScriptLoader, NullQuestScriptLoader>();

        services.AddSingleton<QuestMarkerService>();
        services.AddSingleton<QuestService>();
        services.AddSingleton<QuestObjectiveWatcher>();

        // Help -- resolver wraps World.GetEntity(id).Roles so role-gated help
        // (builder/admin) is visible to entities that actually hold those roles.
        services.AddSingleton<HelpService>(sp => new HelpService(
            sp.GetService<ILogger<HelpService>>(),
            entityId => Guid.TryParse(entityId, out var gid)
                ? sp.GetRequiredService<World>().GetEntity(gid)?.Roles ?? Enumerable.Empty<string>()
                : Enumerable.Empty<string>(),
            sp.GetRequiredService<Tapestry.Engine.Registration.RegistrationPolicy>()));

        services.AddSingleton<HelpSeal>(sp => new HelpSeal(
            sp.GetRequiredService<HelpService>(),
            sp.GetRequiredService<CommandRegistry>(),
            sp.GetRequiredService<Tapestry.Engine.Registration.IPackEdgeOracle>(),
            sp.GetRequiredService<HelpService>().AuthoredWinners,
            sp.GetRequiredService<HelpSealOptions>(),
            sp.GetService<ILogger<HelpSeal>>()));

        // Color / Rendering
        services.AddSingleton<ThemeRegistry>();
        services.AddSingleton<ColorRenderer>();
        services.AddSingleton<Tapestry.Engine.Text.OutputWrapper>();
        services.AddSingleton<Tapestry.Engine.Text.OutputWidthService>();
        services.AddSingleton<PromptRenderer>();
        services.AddSingleton<PanelRenderer>();

        return services;
    }
}
