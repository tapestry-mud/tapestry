using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Tapestry.Authoring;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Mapping;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Quests;
using Tapestry.Engine.Recommend;
using Tapestry.Engine.Tags;
using Tapestry.Scripting.Authoring;
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
        services.AddSingleton<CommandResponseContext>();
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
        services.AddSingleton<IJintApiModule, HelpModule>();
        services.AddSingleton<IJintApiModule, TrainingModule>();
        services.AddSingleton<IJintApiModule, AdminModule>();
        services.AddSingleton<IJintApiModule, RegistryModule>();
        services.AddSingleton<IJintApiModule, CurrencyModule>();
        services.AddSingleton<IJintApiModule, ShopModule>();
        services.AddSingleton<IJintApiModule, OracleModule>();
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
        services.AddSingleton<QuestScriptLoader>();
        services.AddSingleton<IQuestScriptLoader>(sp => sp.GetRequiredService<QuestScriptLoader>());
        services.AddSingleton<QuestModule>();
        services.AddSingleton<IJintApiModule>(sp => sp.GetRequiredService<QuestModule>());
        services.AddSingleton<IJintApiModule, WatchModule>();
        services.AddSingleton<IJintApiModule, ArgsModule>();
        services.AddSingleton<FlowsModule>();
        services.AddSingleton<IJintApiModule>(sp => sp.GetRequiredService<FlowsModule>());
        services.AddSingleton<ScheduleModule>();
        services.AddSingleton<IJintApiModule>(sp => sp.GetRequiredService<ScheduleModule>());

        services.TryAddSingleton<IGmcpModuleAdapter, NullGmcpModuleAdapter>();
        services.AddSingleton<IJintApiModule, GmcpModule>();
        services.AddSingleton<IJintApiModule, RespondModule>();
        services.AddSingleton<IJintApiModule, NotificationsModule>();

        // Runtime and loader
        services.AddSingleton<TagRegistry>();
        services.AddSingleton<Tapestry.Scripting.Interop.PackDependencyGraph>();
        services.AddSingleton<Tapestry.Engine.Registration.IPackEdgeOracle>(sp => sp.GetRequiredService<Tapestry.Scripting.Interop.PackDependencyGraph>());
        services.AddSingleton<MobInvocationBudget>();
        services.AddSingleton<Tapestry.Scripting.Interop.TapestryModuleLoader>(sp =>
            new Tapestry.Scripting.Interop.TapestryModuleLoader(sp.GetRequiredService<Tapestry.Engine.Registration.IPackEdgeOracle>()));
        services.AddSingleton<JintRuntime>();
        services.AddSingleton<PackLoader>();
        services.AddSingleton<IPackManifestProvider>(sp => sp.GetRequiredService<PackLoader>());
        services.AddSingleton<PackValidator>();
        services.AddSingleton<ConnectionLoader>(sp =>
        {
            var world = sp.GetRequiredService<World>();
            var logger = sp.GetRequiredService<ILogger<ConnectionLoader>>();
            var config = sp.GetRequiredService<ServerConfig>();
            var connectionsPath = ResolveDataPath(config.Persistence.ConnectionsPath, config.ConfigDirectory);
            return new ConnectionLoader(world, logger, connectionsPath);
        });
        services.AddSingleton<ConnectionsModule>(sp =>
        {
            var world = sp.GetRequiredService<World>();
            var loader = sp.GetRequiredService<ConnectionLoader>();
            var config = sp.GetRequiredService<ServerConfig>();
            var connectionsPath = ResolveDataPath(config.Persistence.ConnectionsPath, config.ConfigDirectory);
            return new ConnectionsModule(world, loader, connectionsPath);
        });
        services.AddSingleton<IJintApiModule>(sp => sp.GetRequiredService<ConnectionsModule>());

        services.AddSingleton<RoomsModule>();
        services.AddSingleton<IJintApiModule>(sp => sp.GetRequiredService<RoomsModule>());

        // Authoring keystone — recommend seam, room projection, authored-room boot
        // re-apply, and the in-MUD authoring JS module (tapestry.authoring.*).
        services.AddSingleton<RecommendBroker>(sp =>
        {
            var broker = new RecommendBroker();
            var llm = sp.GetRequiredService<ServerConfig>().Llm;
            var provider = BuildRecommendProvider(llm, Environment.GetEnvironmentVariable);
            if (provider != null)
            {
                broker.Register(provider);
            }
            // else: nothing bound -> broker.IsEnabled == false -> consumers hide the affordance.
            return broker;
        });

        // AttributeWriter is registry-driven (PropertyRegistry + TagRegistry). Not
        // registered by AddTapestryEngine, so register it here for the authoring module.
        services.TryAddSingleton<AttributeWriter>();

        services.AddSingleton<RoomProjector>();
        services.AddSingleton<AreaProjector>();

        // Area-map projection + rendering (builder rooms / player map / future GMCP Area.Map)
        services.AddSingleton<AreaMapProjector>();
        services.AddSingleton<AsciiMapRenderer>();

        // Shared live set of loaded pack namespaces. PackLoader fills it during load;
        // WorldAuthoringModule holds the SAME HashSet so a post-boot createRoom sees it.
        services.AddSingleton<LoadedPackNamespaces>();

        services.AddSingleton<AuthoredRoomLoader>(sp =>
        {
            var world = sp.GetRequiredService<World>();
            var logger = sp.GetRequiredService<ILogger<AuthoredRoomLoader>>();
            var config = sp.GetRequiredService<ServerConfig>();
            var roomsRoot = ResolveDataPath(config.Persistence.RoomsPath, config.ConfigDirectory);
            return new AuthoredRoomLoader(
                world, logger, roomsRoot,
                sp.GetRequiredService<PropertyRegistry>(),
                sp.GetRequiredService<TagRegistry>());
        });

        services.AddSingleton<AuthoredAreaLoader>(sp =>
        {
            var config = sp.GetRequiredService<ServerConfig>();
            var areasRoot = ResolveDataPath(config.Persistence.RoomsPath, config.ConfigDirectory); // areas + rooms share the same data/areas root
            var logger = sp.GetRequiredService<ILogger<AuthoredAreaLoader>>();
            return new AuthoredAreaLoader(
                areasRoot,
                sp.GetRequiredService<AreaRegistry>(),
                logger);
        });

        services.AddSingleton<AuthoredOracleLoader>(sp =>
        {
            var config = sp.GetRequiredService<ServerConfig>();
            var areasRoot = ResolveDataPath(config.Persistence.RoomsPath, config.ConfigDirectory); // same data/areas root
            var logger = sp.GetRequiredService<ILogger<AuthoredOracleLoader>>();
            return new AuthoredOracleLoader(
                areasRoot,
                sp.GetRequiredService<OracleTableRegistry>(),
                logger);
        });

        services.AddSingleton<WorldAuthoringModule>(sp =>
        {
            var config = sp.GetRequiredService<ServerConfig>();
            var roomsRoot = ResolveDataPath(config.Persistence.RoomsPath, config.ConfigDirectory);
            return new WorldAuthoringModule(
                sp.GetRequiredService<World>(),
                sp.GetRequiredService<RoomProjector>(),
                sp.GetRequiredService<AttributeWriter>(),
                roomsRoot,
                sp.GetRequiredService<LoadedPackNamespaces>().Namespaces,
                sp.GetRequiredService<AreaRegistry>(),
                sp.GetRequiredService<StubExitResolver>(),
                sp.GetRequiredService<OracleTableRegistry>(),
                sp.GetRequiredService<RecommendBroker>(),
                sp.GetRequiredService<ConnectionLoader>(),
                sp.GetRequiredService<GameLoop>(),
                sp.GetRequiredService<ILogger<WorldAuthoringModule>>(),
                sp.GetRequiredService<TapestryMetrics>());
        });
        services.AddSingleton<IJintApiModule>(sp => sp.GetRequiredService<WorldAuthoringModule>());

        services.AddSingleton<FsModule>(sp =>
        {
            var config = sp.GetRequiredService<ServerConfig>();
            return new FsModule(config.ConfigDirectory);
        });
        services.AddSingleton<IJintApiModule>(sp => sp.GetRequiredService<FsModule>());

        return services;
    }

    /// <summary>Maps the server's llm: config to the Authoring provider config (resolving the
    /// API key from the environment, never YAML) and returns the config-gated provider, or null
    /// when nothing should be bound. <paramref name="getEnv"/> is injectable for tests.</summary>
    internal static IRecommendProvider? BuildRecommendProvider(LlmSection llm, Func<string, string?> getEnv)
    {
        var apiKey = string.IsNullOrEmpty(llm.ApiKeyEnv) ? "" : (getEnv(llm.ApiKeyEnv) ?? "");

        var llmConfig = new RecommendLlmConfig(
            Enabled: llm.Enabled,
            UseStub: llm.UseStub,
            BaseUrl: llm.BaseUrl,
            Model: llm.Model,
            ApiKey: apiKey,
            RequiresKey: llm.RequiresKey,
            Temperature: llm.Temperature,
            MaxSentences: llm.MaxSentences,
            Candidates: llm.Candidates,
            TimeoutSeconds: llm.TimeoutSeconds);

        var promptConfig = new RecommendPromptConfig(
            SystemPrompt: string.IsNullOrWhiteSpace(llm.SystemPrompt)
                ? RecommendPromptConfig.DefaultSystemPrompt
                : llm.SystemPrompt,
            TaskLines: (llm.TaskLines != null && llm.TaskLines.Count > 0)
                ? llm.TaskLines
                : RecommendPromptConfig.DefaultTaskLines,
            MaxSentences: llm.MaxSentences,
            NeighborGuidance: string.IsNullOrWhiteSpace(llm.NeighborGuidance)
                ? RecommendPromptConfig.DefaultNeighborGuidance
                : llm.NeighborGuidance)
        {
            AreaSystemPrompt = string.IsNullOrWhiteSpace(llm.AreaSystemPrompt)
                ? RecommendPromptConfig.DefaultAreaSystemPrompt
                : llm.AreaSystemPrompt
        };

        return LlmProviderFactory.Create(llmConfig, promptConfig, () => new HttpClient());
    }

    private static string ResolveDataPath(string configuredPath, string configDirectory)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }
        if (string.IsNullOrEmpty(configDirectory))
        {
            return Path.GetFullPath(configuredPath);
        }
        return Path.GetFullPath(configuredPath, configDirectory);
    }
}
