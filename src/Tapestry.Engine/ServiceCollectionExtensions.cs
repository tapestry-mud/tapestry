using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
using Tapestry.Engine.Sustenance;
using Tapestry.Engine.Consumables;
using Tapestry.Engine.Rest;
using Tapestry.Engine.Ui;
using Tapestry.Data;

namespace Tapestry.Engine;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTapestryEngine(this IServiceCollection services)
    {
        // Config
        services.TryAddSingleton<ServerConfig>();

        // Persistence
        services.AddSingleton<PropertyTypeRegistry>();
        services.AddSingleton<PlayerSerializer>();

        // Core
        services.AddSingleton<World>();
        services.AddSingleton<EventBus>();
        services.AddSingleton<CommandRegistry>();
        services.AddSingleton<EmoteRegistry>();
        services.AddSingleton<CommandRouter>();
        services.AddSingleton<SessionManager>();
        services.AddSingleton<SystemEventQueue>();
        services.AddSingleton<TapestryMetrics>();
        services.AddSingleton<GameLoop>();
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<ServerConfig>();
            return new TickTimer(1000 / config.Server.TickRateMs);
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

        // Mobs
        services.AddSingleton<SpawnManager>(sp => new SpawnManager(
            sp.GetRequiredService<World>(),
            sp.GetRequiredService<EventBus>(),
            sp.GetRequiredService<LootTableResolver>(),
            sp.GetRequiredService<ItemRegistry>(),
            sp.GetRequiredService<ClassRegistry>(),
            sp.GetRequiredService<RaceRegistry>()));
        services.AddSingleton<DispositionEvaluator>();
        services.AddSingleton<MobAIManager>();

        // Combat
        services.AddSingleton<CombatManager>();

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

        // Sustenance / Consumables
        services.AddSingleton<SustenanceConfig>();
        services.AddSingleton<ConsumableService>();

        // Rest
        services.AddSingleton<RestConfig>();
        services.AddSingleton<RestService>();

        // Doors / Portals / Areas
        services.AddSingleton<DoorService>();
        services.AddSingleton<AreaRegistry>();
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

        // Color / Rendering
        services.AddSingleton<ThemeRegistry>();
        services.AddSingleton<ColorRenderer>();
        services.AddSingleton<PromptRenderer>();
        services.AddSingleton<PanelRenderer>();

        return services;
    }
}
