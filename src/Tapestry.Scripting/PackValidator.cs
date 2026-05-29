using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Engine.Training;
using Tapestry.Scripting.Interop;
using Tapestry.Shared;

namespace Tapestry.Scripting;

public class PackValidator
{
    private readonly SpawnManager _spawnManager;
    private readonly ItemRegistry _itemRegistry;
    private readonly World _world;
    private readonly ILogger<PackValidator> _logger;
    private readonly AbilityRegistry _abilityRegistry;
    private readonly CommandRegistry _commandRegistry;
    private readonly TagRegistry _tagRegistry;
    private readonly IPackManifestProvider _manifestProvider;
    private readonly PropertyRegistry _propertyRegistry;
    private readonly PackDependencyGraph _dependencyGraph;
    private readonly PackExportRegistry _exports;
    private readonly InteropCallSiteRegistry _callSites;

    public PackValidator(
        SpawnManager spawnManager,
        ItemRegistry itemRegistry,
        World world,
        ILogger<PackValidator> logger,
        AbilityRegistry abilityRegistry,
        CommandRegistry commandRegistry,
        TagRegistry tagRegistry,
        IPackManifestProvider manifestProvider,
        PropertyRegistry propertyRegistry,
        PackDependencyGraph dependencyGraph,
        PackExportRegistry exports,
        InteropCallSiteRegistry callSites)
    {
        _spawnManager = spawnManager;
        _itemRegistry = itemRegistry;
        _world = world;
        _logger = logger;
        _abilityRegistry = abilityRegistry;
        _commandRegistry = commandRegistry;
        _tagRegistry = tagRegistry;
        _manifestProvider = manifestProvider;
        _propertyRegistry = propertyRegistry;
        _dependencyGraph = dependencyGraph;
        _exports = exports;
        _callSites = callSites;
    }

    public void Validate()
    {
        var issueCount = 0;

        issueCount += ValidateMobs();
        issueCount += ValidateItems();
        issueCount += ValidateRooms();
        issueCount += ValidateTags();
        issueCount += ValidateProperties();
        ValidateDependenciesPresent();

        _logger.LogInformation("Pack validation complete: {Count} issue(s) found", issueCount);
    }

    private int ValidateMobs()
    {
        var count = 0;

        foreach (var template in _spawnManager.AllTemplates)
        {
            if (template.Tags.Contains("skill_trainer"))
            {
                if (template.TrainerConfig == null)
                {
                    _logger.LogWarning(
                        "Mob {Id} has tag 'skill_trainer' but TrainerConfig is missing or malformed",
                        template.Id);
                    count++;
                    continue;
                }

                if (template.TrainerConfig.AbilityIds.Count == 0)
                {
                    _logger.LogWarning(
                        "Mob {Id} has tag 'skill_trainer' but TrainerConfig.AbilityIds is empty",
                        template.Id);
                    count++;
                }
            }

            if (template.Tags.Contains(ShopProperties.ShopTag))
            {
                if (template.ShopConfig == null || template.ShopConfig.Sells.Count == 0)
                {
                    _logger.LogWarning(
                        "Mob {Id} has tag '{Tag}' but shop config (sells list) is missing",
                        template.Id,
                        ShopProperties.ShopTag);
                    count++;
                }
            }

            var battleCommands = template.BattleCommands;
            var abilities = template.Abilities;

            var abilityLikeCommands = battleCommands
                .Where(c => !string.IsNullOrWhiteSpace(c) && !c.TrimStart().Contains(' '))
                .ToList();
            if (abilityLikeCommands.Count > 0 && abilities.Count == 0)
            {
                _logger.LogWarning(
                    "Mob {Id} has battle commands that look like abilities ({Commands}) but no abilities defined -- they will fizzle",
                    template.Id, string.Join(", ", abilityLikeCommands));
                count++;
            }

            if (battleCommands.Count > 0 && template.Stats.MaxHp == 0)
            {
                _logger.LogWarning(
                    "Mob {Id} has battlecommands but no HP -- mob can't survive combat", template.Id);
                count++;
            }

            foreach (var abilityEntry in abilities)
            {
                if (string.IsNullOrEmpty(abilityEntry.Id)) { continue; }
                var def = _abilityRegistry.Get(abilityEntry.Id);
                if (def == null)
                {
                    _logger.LogWarning(
                        "Mob {Id} references unknown ability {AbilityId}", template.Id, abilityEntry.Id);
                    count++;
                    continue;
                }
                if (def.Category == AbilityCategory.Spell && template.Stats.MaxResource == 0)
                {
                    _logger.LogWarning(
                        "Mob {Id} has spell {AbilityId} but MaxResource is 0", template.Id, abilityEntry.Id);
                    count++;
                }
                if (def.Category == AbilityCategory.Skill && template.Stats.MaxMovement == 0)
                {
                    _logger.LogWarning(
                        "Mob {Id} has skill {AbilityId} but MaxMovement is 0", template.Id, abilityEntry.Id);
                    count++;
                }
            }

            foreach (var cmd in battleCommands)
            {
                if (string.IsNullOrEmpty(cmd)) { continue; }
                var verb = cmd.Split(' ')[0];
                if (_commandRegistry.Resolve(verb) == null)
                {
                    _logger.LogWarning(
                        "Mob {Id} battlecommand \"{Cmd}\" has no matching command in registry",
                        template.Id, verb);
                    count++;
                }
            }
        }

        return count;
    }

    private int ValidateItems()
    {
        var count = 0;

        foreach (var template in _itemRegistry.AllTemplates)
        {
            if (template.SpawnOn.Count == 0) { continue; }

            var packName = template.Id.Contains(':')
                ? template.Id.Split(':', 2)[0]
                : null;

            foreach (var entry in template.SpawnOn)
            {
                var selector = entry.Selector;

                // {tag} — enforce cross-pack dep via TagRegistry resolver
                if (selector.Tag != null)
                {
                    if (!_tagRegistry.TryResolve(selector.Tag, packName, out _))
                    {
                        var msg = $"Item '{template.Id}' spawn_on references tag '{selector.Tag}' " +
                                  $"which is unknown or from another pack not declared as a dependency.";
                        throw new InvalidOperationException(msg);
                    }
                }

                // {id} — if cross-pack, enforce declared dep edge
                if (selector.Id != null && selector.Id.Contains(':'))
                {
                    var targetPack = selector.Id.Split(':', 2)[0];
                    if (!string.Equals(targetPack, packName, StringComparison.OrdinalIgnoreCase)
                        && !_dependencyGraph.DeclaresEdge(packName ?? "", targetPack))
                    {
                        var msg = $"Item '{template.Id}' spawn_on references id '{selector.Id}' " +
                                  $"from pack '{targetPack}' which is not a declared dependency.";
                        throw new InvalidOperationException(msg);
                    }
                }

                // {shop} — recognized + allowed (no dep check). {type} — engine-builtin, no cross-pack concern.
            }
        }

        return count;
    }

    private int ValidateRooms()
    {
        var count = 0;

        foreach (var room in _world.AllRooms)
        {
            _ = room;
        }

        return count;
    }

    private int ValidateTags()
    {
        var count = 0;
        var manifests = _manifestProvider.LoadedPacks
            .ToDictionary(m => PackLoader.PackNamespace(m.Name), m => m, StringComparer.OrdinalIgnoreCase);

        foreach (var template in _spawnManager.AllTemplates)
        {
            count += ValidateEntityTags(template.Id, template.Tags, template.Type, manifests);
        }

        foreach (var template in _itemRegistry.AllTemplates)
        {
            count += ValidateEntityTags(template.Id, template.Tags, "item", manifests);
        }

        foreach (var room in _world.AllRooms)
        {
            count += ValidateEntityTags(room.Id, room.Tags, "room", manifests);
        }

        return count;
    }

    private int ValidateEntityTags(
        string entityId,
        IEnumerable<string> tags,
        string entityType,
        Dictionary<string, PackManifest> manifestsByName)
    {
        var packName = entityId.Contains(':') ? entityId.Split(':', 2)[0] : null;

        if (packName != null && !manifestsByName.ContainsKey(packName))
        {
            _logger.LogWarning(
                "Entity '{EntityId}' belongs to pack '{PackName}' which has no loaded manifest; defaulting to strict validation",
                entityId, packName);
        }

        var lenient = packName != null
            && manifestsByName.TryGetValue(packName, out var manifest)
            && manifest.Validation == "lenient";
        var count = 0;

        foreach (var tag in tags)
        {
            if (!_tagRegistry.TryResolve(tag, packName, out var entry))
            {
                var message = $"Unknown tag '{tag}' on {entityType} '{entityId}'.";
                if (lenient)
                {
                    _logger.LogWarning("{Message}", message);
                    count++;
                }
                else
                {
                    throw new InvalidOperationException(message);
                }
                continue;
            }

            if (!entry.AppliesToType(entityType))
            {
                throw new InvalidOperationException(
                    $"Tag '{tag}' on '{entityId}' is not valid for {entityType} " +
                    $"(applies to: {string.Join(", ", entry.AppliesTo)}).");
            }
        }

        return count;
    }

    private int ValidateProperties()
    {
        var count = 0;

        foreach (var (id, entityType, packName, getKeys, getRaw) in GetAllLoadedEntityData())
        {
            var isLenient = IsLenientPack(packName);
            foreach (var key in getKeys())
            {
                if (!_propertyRegistry.TryResolve(key, packName, out var entry))
                {
                    var message = $"Entity '{id}' (type={entityType}) in pack '{packName}' has unregistered property '{key}'";
                    if (isLenient)
                    {
                        _logger.LogWarning(message);
                        count++;
                    }
                    else
                    {
                        throw new InvalidOperationException(message);
                    }
                    continue;
                }

                if (entry.Transient) { continue; }

                var raw = getRaw(key);
                if (!PropertyValueMatchesType(raw, entry.ValueType))
                {
                    throw new InvalidOperationException(
                        $"Entity '{id}' property '{key}' has wrong type. Expected {entry.ValueType}, got {raw?.GetType().Name ?? "null"} (value: {raw}).");
                }

                if (!entry.AppliesToType(entityType))
                {
                    throw new InvalidOperationException(
                        $"Entity '{id}' (type={entityType}) has property '{key}' which only applies to: {string.Join(", ", entry.AppliesTo ?? (IEnumerable<string>)Array.Empty<string>())}");
                }
            }
        }

        return count;
    }

    // Check 1: every required dependency a pack declares must resolve to a loaded pack.
    // Optional dependencies that are absent are fine. Always fatal — a missing required dep is
    // an unambiguous contract violation, not downgradable by strict/lenient.
    private void ValidateDependenciesPresent()
    {
        foreach (var manifest in _manifestProvider.LoadedPacks)
        {
            var from = PackLoader.PackNamespace(manifest.Name);
            foreach (var depKey in manifest.Dependencies.Keys)
            {
                var dep = PackLoader.PackNamespace(depKey);
                if (!_dependencyGraph.IsLoaded(dep))
                {
                    throw new InvalidOperationException(
                        $"{from}: declares a dependency on {dep}, which is not loaded");
                }
            }
        }
    }

    private readonly HashSet<string> _loggedManifestWarnings = new(StringComparer.OrdinalIgnoreCase);

    private bool IsLenientPack(string? packName)
    {
        if (packName == null) { return false; }
        var manifest = _manifestProvider.LoadedPacks
            .FirstOrDefault(m => PackLoader.PackNamespace(m.Name) == packName);
        if (manifest == null)
        {
            if (_loggedManifestWarnings.Add(packName))
            {
                var loaded = string.Join(", ", _manifestProvider.LoadedPacks.Select(m => $"'{m.Name}' → '{PackLoader.PackNamespace(m.Name)}'"));
                _logger.LogWarning("Pack '{PackName}' has no loaded manifest (loaded: [{Loaded}]); defaulting to strict", packName, loaded);
            }
            return false;
        }
        return manifest.Validation == "lenient";
    }

    private static bool PropertyValueMatchesType(object? raw, PropertyValueType expected)
    {
        if (raw == null) { return true; }
        return expected switch
        {
            PropertyValueType.String => raw is string,
            PropertyValueType.Int => raw is int or long,
            PropertyValueType.Double => raw is double or float or int or long,
            PropertyValueType.Bool => raw is bool,
            PropertyValueType.Long => raw is long or int,
            PropertyValueType.MapInt => raw is Dictionary<string, int>,
            PropertyValueType.MapString => raw is Dictionary<string, string>,
            PropertyValueType.ListString => raw is System.Collections.IList,
            _ => true
        };
    }

    private IEnumerable<(string id, string entityType, string? packName, Func<IEnumerable<string>> getKeys, Func<string, object?> getRaw)> GetAllLoadedEntityData()
    {
        foreach (var template in _spawnManager.AllTemplates)
        {
            var entity = template.CreateEntity();
            var packName = template.Id.Contains(':') ? template.Id.Split(':', 2)[0] : null;
            yield return (template.Id, entity.Type, packName, entity.GetAllPropertyKeys, entity.GetRawProperty);
        }
        foreach (var template in _itemRegistry.AllTemplates)
        {
            var entity = template.CreateEntity();
            var packName = template.Id.Contains(':') ? template.Id.Split(':', 2)[0] : null;
            yield return (template.Id, entity.Type, packName, entity.GetAllPropertyKeys, entity.GetRawProperty);
        }
        foreach (var room in _world.AllRooms)
        {
            var packName = room.Id.Contains(':') ? room.Id.Split(':', 2)[0] : null;
            yield return (room.Id, "room", packName, room.GetAllPropertyKeys, room.GetRawProperty);
        }
    }
}
