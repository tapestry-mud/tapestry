using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Flow;
using Tapestry.Scripting.Connections;
using Tapestry.Scripting.Modules;
using Tapestry.Scripting.Services;

namespace Tapestry.Scripting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTapestryScripting(this IServiceCollection services)
    {
        // Service classes
        services.AddSingleton<ApiMessaging>();
        services.AddSingleton<ApiWorld>();
        services.AddSingleton<ApiStats>();
        services.AddSingleton<ApiMobs>();
        services.AddSingleton<ApiTransfer>();

        // Modules — registered as IJintApiModule so JintRuntime discovers them
        // CommandsModule also registered as its concrete type for direct injection
        services.AddSingleton<CommandsModule>();
        services.AddSingleton<IJintApiModule>(sp => sp.GetRequiredService<CommandsModule>());
        services.AddSingleton<IJintApiModule, EmotesModule>();
        services.AddSingleton<IJintApiModule, EventsModule>();
        services.AddSingleton<IJintApiModule, WorldModule>();
        services.AddSingleton<IJintApiModule, StatsModule>();
        services.AddSingleton<IJintApiModule, InventoryModule>();
        services.AddSingleton<IJintApiModule, EquipmentModule>();
        services.AddSingleton<IJintApiModule, ItemsModule>();
        services.AddSingleton<IJintApiModule, CombatModule>();
        services.AddSingleton<IJintApiModule, ProgressionModule>();
        services.AddSingleton<IJintApiModule, MobsModule>();
        services.AddSingleton<IJintApiModule, ThemeModule>();
        services.AddSingleton<IJintApiModule, DiceModule>();
        services.AddSingleton<IJintApiModule, AbilitiesModule>();
        services.AddSingleton<IJintApiModule, EffectsModule>();
        services.AddSingleton<IJintApiModule, ClassesModule>();
        services.AddSingleton<IJintApiModule, RacesModule>();
        services.AddSingleton<IJintApiModule, AlignmentModule>();
        services.AddSingleton<IJintApiModule, UiModule>();
        services.AddSingleton<IJintApiModule, TrainingModule>();
        services.AddSingleton<IJintApiModule, AdminModule>();
        services.AddSingleton<IJintApiModule, CurrencyModule>();
        services.AddSingleton<IJintApiModule, ShopModule>();
        services.AddSingleton<IJintApiModule, ConsumablesModule>();
        services.AddSingleton<IJintApiModule, RestModule>();
        services.AddSingleton<IJintApiModule, DoorsModule>();
        services.AddSingleton<IJintApiModule, PortalsModule>();
        services.AddSingleton<IJintApiModule, AreaModule>();
        services.AddSingleton<IJintApiModule, TimeModule>();
        services.AddSingleton<IJintApiModule, WeatherModule>();
        services.AddSingleton<IJintApiModule, ReturnAddressModule>();
        services.AddSingleton<PackContext>();
        services.AddSingleton<IJintApiModule, DataModule>();
        services.AddSingleton<IJintApiModule, RarityModule>();
        services.AddSingleton<IJintApiModule, EssenceModule>();
        services.AddSingleton<IJintApiModule, StackingModule>();
        services.AddSingleton<IJintApiModule, PacksModule>();
        services.AddSingleton<FlowsModule>();
        services.AddSingleton<IJintApiModule>(sp => sp.GetRequiredService<FlowsModule>());

        services.TryAddSingleton<IGmcpModuleAdapter, NullGmcpModuleAdapter>();
        services.AddSingleton<IJintApiModule, GmcpModule>();

        // Runtime and loader
        services.AddSingleton<JintRuntime>();
        services.AddSingleton<PackLoader>();
        services.AddSingleton<ConnectionLoader>(sp =>
        {
            var world = sp.GetRequiredService<World>();
            var logger = sp.GetRequiredService<ILogger<ConnectionLoader>>();
            var config = sp.GetRequiredService<ServerConfig>();
            return new ConnectionLoader(world, logger, config.ConfigDirectory);
        });

        return services;
    }
}
