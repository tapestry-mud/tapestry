using Microsoft.Extensions.Logging;
using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Color;
using Tapestry.Engine.Help;
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
    private readonly CommandRegistry _commandRegistry;
    private readonly HelpService _helpService;
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
        CommandRegistry commandRegistry,
        HelpService helpService,
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
        _commandRegistry = commandRegistry;
        _helpService = helpService;
        _commandsModule = commandsModule;
        _logger = logger;
    }

    public void Configure()
    {
        LoadPacks();
        _abilityCommandBridge.WireAll();
        _packValidator.Validate();
        _connectionLoader.Load();
        var motd = !string.IsNullOrWhiteSpace(_config.Server.Motd)
            ? _config.Server.Motd
            : _packLoader.PackMotd ?? "Welcome to Tapestry!";
        _messaging.SetMotd(motd);
        AppendPackCreditsToMotd();

        // Auto-generate help topics for commands with ArgDefinitions.
        // Pack help files (higher loadOrder from LoadPack) override these.
        var helpRegistrations = _commandRegistry.PrimaryKeywords
            .Select(k => _commandRegistry.Resolve(k))
            .Where(r => r != null)
            .Select(r => r!)
            .DistinctBy(r => r.Keyword, StringComparer.OrdinalIgnoreCase);
        CommandHelpGenerator.GenerateAll(helpRegistrations, _helpService);

        _commandsModule.LogLoadTimeWarnings();
        _themeRegistry.Compile();
    }

    private void LoadPacks()
    {
        var packsDir = Path.Combine(AppContext.BaseDirectory, "packs");
        if (!Directory.Exists(packsDir))
        {
            _packLoader.ValidateAreaWeatherZones();
            return;
        }

        var packDirs = DiscoverPackDirectories(packsDir);

        if (packDirs.Count == 0 && _config.Packs.Count > 0)
        {
            // Fall back to explicit list from server.yaml
            foreach (var packName in _config.Packs)
            {
                var packDir = Path.Combine(packsDir, packName);
                if (Directory.Exists(packDir)) { packDirs.Add(packDir); }
                else { _logger.LogWarning("Pack not found: {Pack} (looked in {Dir})", packName, packDir); }
            }
        }

        // Phase 1: all packs declare tags, properties, and slots
        var manifests = packDirs.Select(dir => _packLoader.LoadDeclarations(dir)).ToList();

        // Phase 2: all packs load content (rooms, items, mobs, scripts, help, themes)
        foreach (var (dir, manifest) in packDirs.Zip(manifests))
        {
            _packLoader.LoadContent(dir, manifest);
            _logger.LogInformation("Loaded pack: {Pack}", Path.GetRelativePath(packsDir, dir));
        }

        _packLoader.ValidateAreaWeatherZones();
    }

    private static List<string> DiscoverPackDirectories(string packsDir)
    {
        var dirs = new List<string>();

        foreach (var entry in Directory.EnumerateDirectories(packsDir).OrderBy(d => d))
        {
            var name = Path.GetFileName(entry);
            if (name.StartsWith('@'))
            {
                // Scoped: @scope/package-name
                foreach (var scoped in Directory.EnumerateDirectories(entry).OrderBy(d => d))
                {
                    dirs.Add(scoped);
                }
            }
            else
            {
                dirs.Add(entry);
            }
        }

        return dirs;
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
