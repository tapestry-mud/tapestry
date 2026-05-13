using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Color;
using Tapestry.Engine.Help;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Stats;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Tags;
using Tapestry.Engine.Training;
using Tapestry.Scripting.Properties;
using Tapestry.Scripting.Tags;
using Tapestry.Shared;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Scripting;

public class PackLoader : IPackManifestProvider
{
    private readonly World _world;
    private readonly SlotRegistry _slotRegistry;
    private readonly JintRuntime _runtime;
    private readonly ThemeRegistry _theme;
    private readonly SpawnManager _spawnManager;
    private readonly ItemRegistry _itemRegistry;
    private readonly ILogger<PackLoader> _logger;
    private readonly PackContext _packContext;
    private readonly AreaRegistry _areaRegistry;
    private readonly WeatherZoneRegistry _weatherZoneRegistry;
    private readonly HelpService _helpService;
    private readonly TagRegistry _tagRegistry;
    private readonly PropertyRegistry _propertyRegistry;
    private readonly List<(string RoomId, string ItemId)> _pendingFixtures = new();
    private readonly Dictionary<string, string> _registeredEntityFiles = new();

    public List<PackManifest> LoadedPacks { get; } = new();
    IReadOnlyList<PackManifest> IPackManifestProvider.LoadedPacks => LoadedPacks;

    public PackLoader(World world, SlotRegistry slotRegistry, JintRuntime runtime,
                     ThemeRegistry theme, SpawnManager spawnManager, ItemRegistry itemRegistry,
                     ILogger<PackLoader> logger, PackContext packContext,
                     AreaRegistry areaRegistry, WeatherZoneRegistry weatherZoneRegistry,
                     HelpService helpService, TagRegistry tagRegistry,
                     PropertyRegistry propertyRegistry)
    {
        _world = world;
        _slotRegistry = slotRegistry;
        _runtime = runtime;
        _theme = theme;
        _spawnManager = spawnManager;
        _itemRegistry = itemRegistry;
        _logger = logger;
        _packContext = packContext;
        _areaRegistry = areaRegistry;
        _weatherZoneRegistry = weatherZoneRegistry;
        _helpService = helpService;
        _tagRegistry = tagRegistry;
        _propertyRegistry = propertyRegistry;
    }

    // "@tapestry/core" -> "tapestry-core", "my-pack" -> "my-pack"
    public static string PackNamespace(string name) =>
        name.Contains('/')
            ? name.TrimStart('@').Replace('/', '-')
            : name;

    public PackManifest LoadDeclarations(string packDirectory)
    {
        _packContext.CurrentPackDir = packDirectory;
        var manifestPath = Path.Combine(packDirectory, "pack.yaml");
        if (!File.Exists(manifestPath))
        {
            manifestPath = Path.Combine(packDirectory, "tapestry.yaml");
        }
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"No pack.yaml or tapestry.yaml found in {packDirectory}");
        }

        var manifestYaml = File.ReadAllText(manifestPath);
        var manifest = YamlContentLoader.LoadManifest(manifestYaml);
        var packNamespace = PackNamespace(manifest.Name);

        if (!manifest.Active)
        {
            _logger.LogInformation("Skipping inactive pack: {Name}", manifest.Name);
            return manifest;
        }

        _logger.LogInformation("Loading declarations for pack: {Name} v{Version}", manifest.Name, manifest.Version);
        LoadedPacks.Add(manifest);

        TagsFileLoader.LoadIntoRegistry(packDirectory, packNamespace, _tagRegistry);
        PropertiesFileLoader.LoadIntoRegistry(packDirectory, packNamespace, _propertyRegistry);

        if (!string.IsNullOrEmpty(manifest.Content.EquipmentSlots))
        {
            LoadEquipmentSlots(packDirectory, manifest.Content.EquipmentSlots, packNamespace);
        }

        return manifest;
    }

    public void LoadContent(string packDirectory, PackManifest manifest)
    {
        if (!manifest.Active) { return; }

        var packNamespace = PackNamespace(manifest.Name);
        _packContext.CurrentPackDir = packDirectory;
        _packContext.CurrentPackNamespace = packNamespace;

        if (!string.IsNullOrEmpty(manifest.Content.WeatherZones))
        {
            LoadWeatherZones(packDirectory, manifest.Content.WeatherZones);
        }

        if (!string.IsNullOrEmpty(manifest.Content.AreaDefinitions))
        {
            LoadAreaDefinitions(packDirectory, manifest.Content.AreaDefinitions);
        }

        if (!string.IsNullOrEmpty(manifest.Content.Rooms))
        {
            LoadRooms(packDirectory, manifest.Content.Rooms, packNamespace);
        }

        foreach (var room in _world.AllRooms.Where(r => r.Area != null))
        {
            if (!_areaRegistry.Contains(room.Area!))
            {
                throw new InvalidOperationException(
                    $"Room '{room.Id}' references undefined area '{room.Area}'");
            }
        }

        if (!string.IsNullOrEmpty(manifest.Content.Items))
        {
            LoadItems(packDirectory, manifest.Content.Items);
        }

        PlaceFixtures();

        if (!string.IsNullOrEmpty(manifest.Content.Strings))
        {
            LoadThemes(packDirectory, manifest.Content.Strings);
        }

        if (!string.IsNullOrEmpty(manifest.Content.Mobs))
        {
            LoadMobs(packDirectory, manifest.Content.Mobs);
        }

        if (!string.IsNullOrEmpty(manifest.Content.Scripts))
        {
            LoadScripts(packDirectory, manifest.Content.Scripts, packNamespace);
        }

        if (!string.IsNullOrEmpty(manifest.Content.Help))
        {
            _helpService.LoadPack(packNamespace, packDirectory, manifest.Content.Help, manifest.LoadOrder);
        }
    }

    public PackManifest Load(string packDirectory)
    {
        var manifest = LoadDeclarations(packDirectory);
        LoadContent(packDirectory, manifest);
        return manifest;
    }

    private void LoadRooms(string packDir, string glob, string packName = "")
    {
        var allRooms = new List<Room>();
        var areaResetIntervals = new Dictionary<string, int>();

        foreach (var area in _areaRegistry.All())
        {
            areaResetIntervals[area.Id] = area.ResetInterval;
        }

        foreach (var file in MatchFiles(packDir, glob))
        {
            var yaml = File.ReadAllText(file);
            var result = YamlContentLoader.LoadRoom(yaml, _propertyRegistry);
            var room = result.Room;

            ValidateEntityId(room.Id, file);
            if (!string.IsNullOrEmpty(packName)) { room.SetProperty("source_pack", packName); }
            _world.AddRoom(room);
            allRooms.Add(room);
            _logger.LogDebug("  Room: {RoomId}", room.Id);

            var filename = Path.GetFileNameWithoutExtension(file);
            var shortId = room.Id.Contains(':') ? room.Id[(room.Id.LastIndexOf(':') + 1)..] : room.Id;
            if (!string.Equals(filename, shortId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Filename mismatch: {File} declares id '{Id}'", file, room.Id);
            }

            foreach (var fixtureId in result.Fixtures)
            {
                _pendingFixtures.Add((room.Id, fixtureId));
            }

            if (result.Spawns.Count > 0)
            {
                var areaId = room.Area ?? "";
                var effectiveInterval = result.ResetInterval
                    ?? (areaResetIntervals.TryGetValue(areaId, out var ai) ? ai : 300);

                _spawnManager.RegisterRoomSpawns(
                    areaId,
                    room.Id,
                    result.Spawns.Select(s => (s.Mob, s.Count, s.Rare, (IEnumerable<string>)s.Tags)),
                    effectiveInterval);
            }
        }

        YamlContentLoader.MirrorDoorsAcrossRooms(allRooms);
    }

    private void LoadItems(string packDir, string glob)
    {
        foreach (var file in MatchFiles(packDir, glob))
        {
            var yaml = File.ReadAllText(file);
            var itemDef = YamlContentLoader.LoadItem(yaml, _propertyRegistry);

            ValidateEntityId(itemDef.Id, file);

            var filename = Path.GetFileNameWithoutExtension(file);
            var shortId = itemDef.Id.Contains(':') ? itemDef.Id[(itemDef.Id.LastIndexOf(':') + 1)..] : itemDef.Id;
            if (!string.Equals(filename, shortId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Filename mismatch: {File} declares id '{Id}'", file, itemDef.Id);
            }

            var template = new ItemTemplate
            {
                Id = itemDef.Id,
                Name = itemDef.Name,
                Type = itemDef.Type,
                Tags = new List<string>(itemDef.Tags),
                Keywords = new List<string>(itemDef.Keywords),
                Properties = itemDef.Properties.ToDictionary(kv => kv.Key, kv => (object?)kv.Value),
                Modifiers = itemDef.Modifiers.Select(m => new ItemTemplate.ModifierEntry
                {
                    Stat = m.Stat,
                    Value = m.Value
                }).ToList()
            };
            _itemRegistry.Register(template);
            _logger.LogDebug("  Item template: {Id}", template.Id);
        }
    }

    private void PlaceFixtures()
    {
        foreach (var (roomId, itemId) in _pendingFixtures)
        {
            var entity = _itemRegistry.CreateItem(itemId);
            var room = _world.GetRoom(roomId);
            if (entity == null)
            {
                _logger.LogWarning("Fixture item not found: {ItemId} for room {RoomId}", itemId, roomId);
                continue;
            }
            if (room == null)
            {
                _logger.LogWarning("Fixture room not found: {RoomId}", roomId);
                continue;
            }
            room.AddEntity(entity);
            _world.TrackEntity(entity);
            _logger.LogDebug("  Fixture placed: {Item} in {Room}", itemId, roomId);
        }
        _pendingFixtures.Clear();
    }

    private void LoadMobs(string packDir, string glob)
    {
        foreach (var file in MatchFiles(packDir, glob))
        {
            var yaml = File.ReadAllText(file);
            var (template, lootTable) = YamlContentLoader.LoadMob(yaml, _propertyRegistry);

            ValidateEntityId(template.Id, file);

            var filename = Path.GetFileNameWithoutExtension(file);
            var shortId = template.Id.Contains(':') ? template.Id[(template.Id.LastIndexOf(':') + 1)..] : template.Id;
            if (!string.Equals(filename, shortId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Filename mismatch: {File} declares id '{Id}'", file, template.Id);
            }

            if (template.Tags.Contains("skill_trainer"))
            {
                if (!template.Properties.TryGetValue("trains", out var trainsRawCheck))
                {
                    _logger.LogWarning("Mob {Id} has skill_trainer tag but no 'trains' block", template.Id);
                }
                else if (trainsRawCheck is not Dictionary<string, object>)
                {
                    _logger.LogWarning("Mob {Id} 'trains' block has unexpected type {Type} -- trainer config not loaded",
                        template.Id, trainsRawCheck?.GetType().Name ?? "null");
                }
            }

            if (template.Tags.Contains("skill_trainer")
                && template.Properties.TryGetValue("trains", out var trainsRaw)
                && trainsRaw is Dictionary<string, object> trainsDict)
            {
                var tierStr = trainsDict.GetValueOrDefault("tier")?.ToString() ?? "apprentice";
                var tier = tierStr.ToLower() switch
                {
                    "apprentice" => CapTier.Apprentice,
                    "journeyman" => CapTier.Journeyman,
                    "master" => CapTier.Master,
                    _ => CapTier.Apprentice
                };
                var abilities = new List<string>();
                if (trainsDict.TryGetValue("abilities", out var abilitiesRaw)
                    && abilitiesRaw is List<object> abilitiesList)
                {
                    foreach (var a in abilitiesList)
                    {
                        abilities.Add(a.ToString()!);
                    }
                }
                template.Properties.Remove("trains");
                template.TrainerConfig = new TrainerConfig(tier, abilities);
            }

            if (template.Tags.Contains(ShopProperties.ShopTag)
                && template.Properties.TryGetValue("shop", out var shopRaw)
                && shopRaw is Dictionary<object, object> shopDict)
            {
                var sells = new List<string>();
                if (shopDict.TryGetValue("sells", out var sellsRaw)
                    && sellsRaw is List<object> sellsList)
                {
                    sells = sellsList.Select(s => s.ToString()!).ToList();
                }

                var buyMarkup = shopDict.TryGetValue("buy_markup", out var markupRaw)
                    ? Convert.ToDouble(markupRaw)
                    : 0.0;
                var sellDiscount = shopDict.TryGetValue("sell_discount", out var discountRaw)
                    ? Convert.ToDouble(discountRaw)
                    : 0.0;

                template.ShopConfig = new ShopConfig(sells, buyMarkup, sellDiscount);
                template.Properties.Remove("shop");
            }

            if (lootTable != null)
            {
                _spawnManager.RegisterLootTable(lootTable);
                _logger.LogDebug("  Loot table: {Id}", lootTable.Id);
            }

            _spawnManager.RegisterTemplate(template);
            _logger.LogDebug("  Mob template: {Id}", template.Id);
        }
    }

    private void ValidateEntityId(string entityId, string filePath)
    {
        if (_registeredEntityFiles.TryGetValue(entityId, out var existingFile))
        {
            throw new InvalidOperationException(
                $"Duplicate entity ID '{entityId}' declared in:\n  {existingFile}\n  {filePath}");
        }
        _registeredEntityFiles[entityId] = filePath;

        var expectedNs = _packContext.CurrentPackNamespace;
        if (!string.IsNullOrEmpty(expectedNs) && entityId.Contains(':'))
        {
            var ns = entityId[..entityId.IndexOf(':')];
            if (ns != expectedNs)
            {
                throw new InvalidOperationException(
                    $"Namespace mismatch: pack '{expectedNs}' declared ID '{entityId}' in {filePath}");
            }
        }
    }

    private void LoadThemes(string packDir, string glob)
    {
        var files = MatchFiles(packDir, glob);
        foreach (var file in files)
        {
            if (Path.GetFileName(file).Equals("theme.yaml", StringComparison.OrdinalIgnoreCase))
            {
                var yaml = File.ReadAllText(file);
                var entries = YamlContentLoader.LoadTheme(yaml);
                foreach (var (tag, entry) in entries)
                {
                    _theme.Register(tag, new ThemeEntry { Fg = entry.Fg, Bg = entry.Bg });
                    _logger.LogDebug("  Theme: {Tag}", tag);
                }
            }
        }
    }

    private void LoadScripts(string packDir, string glob, string packName)
    {
        var files = MatchFiles(packDir, glob).ToList();

        var initFile = files.FirstOrDefault(f => Path.GetFileName(f) == "init.js");
        if (initFile != null)
        {
            var relative = Path.GetRelativePath(packDir, initFile).Replace('\\', '/');
            _logger.LogDebug("  Script (init): {File}", relative);
            _runtime.Execute(File.ReadAllText(initFile), packName, relative);
            files = files.Where(f => f != initFile).ToList();
        }

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(packDir, file).Replace('\\', '/');
            _logger.LogDebug("  Script: {File}", relative);
            _runtime.Execute(File.ReadAllText(file), packName, relative);
        }
    }

    private void LoadEquipmentSlots(string packDir, string path, string packNamespace)
    {
        var fullPath = Path.Combine(packDir, path);
        if (!File.Exists(fullPath))
        {
            return;
        }

        var yaml = File.ReadAllText(fullPath);
        var slots = YamlContentLoader.LoadEquipmentSlots(yaml);
        foreach (var slotDef in slots)
        {
            if (packNamespace == "tapestry-core")
            {
                _slotRegistry.RegisterEngineSlot(slotDef.Name, slotDef.Display, slotDef.Max);
            }
            else
            {
                _slotRegistry.RegisterPackSlot(packNamespace, slotDef.Name, slotDef.Display, slotDef.Max);
            }
            _logger.LogDebug("  Slot: {Name} (max {Max})", slotDef.Name, slotDef.Max);
        }
    }

    private void LoadAreaDefinitions(string packDir, string glob)
    {
        foreach (var file in MatchFiles(packDir, glob))
        {
            var yaml = File.ReadAllText(file);
            var def = YamlContentLoader.LoadAreaDefinition(yaml);
            _areaRegistry.Register(def);
            _logger.LogDebug("  Area: {Id}", def.Id);
        }
    }

    private void LoadWeatherZones(string packDir, string glob)
    {
        foreach (var file in MatchFiles(packDir, glob))
        {
            var yaml = File.ReadAllText(file);
            var zones = YamlContentLoader.LoadWeatherZones(yaml);
            foreach (var zone in zones)
            {
                _weatherZoneRegistry.Register(zone);
                _logger.LogDebug("  WeatherZone: {Id}", zone.Id);
            }
        }
    }

    public void ValidateAreaWeatherZones()
    {
        foreach (var area in _areaRegistry.All())
        {
            if (area.WeatherZone != null && !_weatherZoneRegistry.Contains(area.WeatherZone))
            {
                throw new InvalidOperationException(
                    $"Area '{area.Id}' references undefined weather zone '{area.WeatherZone}'");
            }
        }
    }

    private static IEnumerable<string> MatchFiles(string baseDir, string glob)
    {
        var matcher = new Matcher();
        matcher.AddInclude(glob);
        var result = matcher.GetResultsInFullPath(baseDir);
        return result.OrderBy(f => f);
    }
}
