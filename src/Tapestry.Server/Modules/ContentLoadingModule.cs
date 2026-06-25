using Microsoft.Extensions.Logging;
using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Distribution;
using Tapestry.Engine.Items;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Scripting;
using Tapestry.Scripting.Authoring;
using Tapestry.Scripting.Connections;
using Tapestry.Scripting.Interop;
using Tapestry.Scripting.Services;

namespace Tapestry.Server.Modules;

public class ContentLoadingModule : IGameModule
{
    private readonly ServerConfig _config;
    private readonly ApiMessaging _messaging;
    private readonly PackLoader _packLoader;
    private readonly ConnectionLoader _connectionLoader;
    private readonly AuthoredRoomLoader _authoredRoomLoader;
    private readonly AuthoredAreaLoader _authoredAreaLoader;
    private readonly AuthoredOracleLoader _authoredOracleLoader;
    private readonly AuthoredItemLoader _authoredItemLoader;
    private readonly TagRegistry _tagRegistry;
    private readonly PropertyRegistry _propertyRegistry;
    private readonly PackDependencyGraph _dependencyGraph;
    private readonly TapestryModuleLoader _moduleLoader;
    private readonly ItemRegistry _itemRegistry;
    private readonly DistributionService _distributionService;
    private readonly ILogger<ContentLoadingModule> _logger;

    public string Name => "ContentLoading";

    public ContentLoadingModule(
        ServerConfig config,
        ApiMessaging messaging,
        PackLoader packLoader,
        ConnectionLoader connectionLoader,
        AuthoredRoomLoader authoredRoomLoader,
        AuthoredAreaLoader authoredAreaLoader,
        AuthoredOracleLoader authoredOracleLoader,
        AuthoredItemLoader authoredItemLoader,
        TagRegistry tagRegistry,
        PropertyRegistry propertyRegistry,
        PackDependencyGraph dependencyGraph,
        TapestryModuleLoader moduleLoader,
        ItemRegistry itemRegistry,
        DistributionService distributionService,
        ILogger<ContentLoadingModule> logger)
    {
        _config = config;
        _messaging = messaging;
        _packLoader = packLoader;
        _connectionLoader = connectionLoader;
        _authoredRoomLoader = authoredRoomLoader;
        _authoredAreaLoader = authoredAreaLoader;
        _authoredOracleLoader = authoredOracleLoader;
        _authoredItemLoader = authoredItemLoader;
        _tagRegistry = tagRegistry;
        _propertyRegistry = propertyRegistry;
        _dependencyGraph = dependencyGraph;
        _moduleLoader = moduleLoader;
        _itemRegistry = itemRegistry;
        _distributionService = distributionService;
        _logger = logger;
    }

    public void Configure()
    {
        LoadPacks();
        // AbilityCommandBridge.WireAll and PackValidator.Validate moved to
        // GameLoopService.StartAsync, AFTER RegistrationPolicy.Resolve(): abilities (and
        // commands) only commit to their registries at the seal barrier — running them
        // here would see empty registries (no ability commands wired, spurious
        // unknown-ability/unknown-command validation warnings).

        // Initialize distribution cache and seed initial room scatter
        _distributionService.Initialize(_itemRegistry.AllTemplates);
        _distributionService.SeedAllRooms();

        // Authored side-cars overlay packed content. Areas before rooms (parents first);
        // both after packs so SourcePack is already stamped for [pack +edits] overlay.
        // Oracle tables load after areas (same root); pack oracle: globs cover tables inside
        // pack dirs only, so authored tables frozen to data/areas/** need this separate pass.
        _authoredAreaLoader.Load();
        _authoredRoomLoader.Load();
        _authoredOracleLoader.Load();
        _authoredItemLoader.Load();   // re-register frozen item side-cars (mirrors the oracle loader)
        _connectionLoader.Load();
        var motd = !string.IsNullOrWhiteSpace(_config.Server.Motd)
            ? _config.Server.Motd
            : _packLoader.PackMotd ?? "Welcome to Tapestry!";
        _messaging.SetMotd(motd);

        var motdColor = _packLoader.PackMotdColor ?? "";
        _messaging.SetMotdColor(motdColor);

        AppendPackCreditsToMotd();

        // Auto-gen command help is gap-filled in HelpSeal AFTER the seal (GameLoopService.StartAsync),
        // because commands are only resolved at the seal; running it here would see an empty registry.

        // ThemeRegistry.Compile() moved to GameLoopService.StartAsync, AFTER
        // RegistrationPolicy.Resolve(): theme tags (theme.register / theme.yaml, plus the
        // rarity/essence item.*/essence.* side-effects) only commit at the seal barrier --
        // compiling here would bake an empty (or partial) lookup and committed tags would
        // never resolve to ANSI codes.
    }

    private void WireDependencyResolvers()
    {
        var depMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifest in _packLoader.LoadedPacks)
        {
            var ns = PackLoader.PackNamespace(manifest.Name);
            var deps = manifest.Dependencies.Keys
                .Concat(manifest.OptionalDependencies.Keys) // §5: optional edges count too
                .Select(PackLoader.PackNamespace)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            depMap[ns] = deps;
        }

        IEnumerable<string> Resolve(string packNamespace) =>
            depMap.TryGetValue(packNamespace, out var deps) ? deps : [];

        _tagRegistry.SetDependencyResolver(Resolve);
        _propertyRegistry.SetDependencyResolver(Resolve);
        _dependencyGraph.Build(depMap); // feed the interop edge gate
        _moduleLoader.BuildFromManifests(_packLoader.LoadedPacks);
    }

    private void LoadPacks()
    {
        var packsDir = _config.ResolvedPacksDirectory;
        if (!Directory.Exists(packsDir))
        {
            _packLoader.ValidateAreaWeatherZones();
            return;
        }

        var packDirs = DiscoverPackDirectories(packsDir);

        if (_config.Packs.Count > 0)
        {
            // Filter discovered packs to only those listed in server.yaml.
            // Matches by scoped name (@mallek/legends-forgotten), namespace (mallek-legends-forgotten), or folder name (legends-forgotten).
            var allowed = new HashSet<string>(_config.Packs, StringComparer.OrdinalIgnoreCase);
            packDirs = packDirs.Where(dir =>
            {
                var rel = Path.GetRelativePath(packsDir, dir).Replace('\\', '/');
                var ns = PackLoader.PackNamespace(rel);
                var folder = Path.GetFileName(dir);
                return allowed.Contains(rel) || allowed.Contains(ns) || allowed.Contains(folder);
            }).ToList();
        }

        // Phase 1: all packs declare tags, properties, and slots
        var manifests = packDirs.Select(dir => _packLoader.LoadDeclarations(dir)).ToList();

        // Wire dependency resolvers so Phase 2 YAML coercion can see dependency properties
        WireDependencyResolvers();

        // Phase 2: all packs load content (rooms, items, mobs, scripts, help, themes)
        // in dependency order — every pack loads after the dependencies it declares,
        // so load-time cross-pack interop (e.g. tapestry.packs.call into a dependency's
        // exports) sees those exports already registered. Independent packs keep their
        // legacy alphabetical order. (Directory order alone loaded @mallek before
        // @tapestry, breaking world packs that depend on @tapestry/*.)
        var topoRank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var topoOrder = _dependencyGraph.TopologicalOrder();
        for (var i = 0; i < topoOrder.Count; i++)
        {
            topoRank[topoOrder[i]] = i;
        }

        var orderedPacks = packDirs.Zip(manifests, (dir, manifest) => (dir, manifest))
            .OrderBy(p => topoRank.TryGetValue(PackLoader.PackNamespace(p.manifest.Name), out var r) ? r : int.MaxValue)
            .ToList();

        foreach (var (dir, manifest) in orderedPacks)
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

        var creditsLine = $"\r\n[ Packs: {credits} ]";
        _messaging.SetMotd(_messaging.GetMotd() + creditsLine);

        var currentColor = _messaging.GetMotdColor();
        if (!string.IsNullOrEmpty(currentColor))
        {
            _messaging.SetMotdColor(currentColor + creditsLine);
        }
    }
}
