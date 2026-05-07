using Microsoft.Extensions.Logging;
using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Color;
using Tapestry.Scripting;
using Tapestry.Scripting.Connections;
using Tapestry.Scripting.Services;

namespace Tapestry.Server.Modules;

public class ContentLoadingModule : IGameModule
{
    private readonly ServerConfig _config;
    private readonly ApiMessaging _messaging;
    private readonly PackLoader _packLoader;
    private readonly PackValidator _packValidator;
    private readonly ConnectionLoader _connectionLoader;
    private readonly ThemeRegistry _themeRegistry;
    private readonly AbilityCommandBridge _abilityCommandBridge;
    private readonly Tapestry.Scripting.Modules.CommandsModule _commandsModule;
    private readonly ILogger<ContentLoadingModule> _logger;

    public string Name => "ContentLoading";

    public ContentLoadingModule(
        ServerConfig config,
        ApiMessaging messaging,
        PackLoader packLoader,
        PackValidator packValidator,
        ConnectionLoader connectionLoader,
        ThemeRegistry themeRegistry,
        AbilityCommandBridge abilityCommandBridge,
        Tapestry.Scripting.Modules.CommandsModule commandsModule,
        ILogger<ContentLoadingModule> logger)
    {
        _config = config;
        _messaging = messaging;
        _packLoader = packLoader;
        _packValidator = packValidator;
        _connectionLoader = connectionLoader;
        _themeRegistry = themeRegistry;
        _abilityCommandBridge = abilityCommandBridge;
        _commandsModule = commandsModule;
        _logger = logger;
    }

    public void Configure()
    {
        _messaging.SetMotd(_config.Server.Motd);
        LoadPacks();
        _packValidator.Validate();
        _connectionLoader.Load();
        AppendPackCreditsToMotd();
        _abilityCommandBridge.WireAll();
        _commandsModule.LogLoadTimeWarnings();
        _themeRegistry.Compile();
    }

    private void LoadPacks()
    {
        var packsDir = Path.Combine(AppContext.BaseDirectory, "packs");

        foreach (var packName in _config.Packs)
        {
            var packDir = Path.Combine(packsDir, packName);
            if (Directory.Exists(packDir))
            {
                _packLoader.Load(packDir);
                _logger.LogInformation("Loaded pack: {Pack}", packName);
            }
            else
            {
                _logger.LogWarning("Pack not found: {Pack} (looked in {Dir})", packName, packDir);
            }
        }

        _packLoader.ValidateAreaWeatherZones();
    }

    private void AppendPackCreditsToMotd()
    {
        var packs = _packLoader.LoadedPacks;
        if (packs.Count == 0) { return; }

        var credits = string.Join(", ", packs.Select(p =>
        {
            var label = string.IsNullOrEmpty(p.DisplayName) ? p.Name : p.DisplayName;
            return string.IsNullOrEmpty(p.Author) ? $"{label} v{p.Version}" : $"{label} v{p.Version} by {p.Author}";
        }));

        var current = _messaging.GetMotd();
        _messaging.SetMotd(current + $"\r\n[ Packs: {credits} ]");
    }
}
