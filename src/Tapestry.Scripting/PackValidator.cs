using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Tags;
using Tapestry.Engine.Training;
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

    public PackValidator(
        SpawnManager spawnManager,
        ItemRegistry itemRegistry,
        World world,
        ILogger<PackValidator> logger,
        AbilityRegistry abilityRegistry,
        CommandRegistry commandRegistry,
        TagRegistry tagRegistry,
        IPackManifestProvider manifestProvider)
    {
        _spawnManager = spawnManager;
        _itemRegistry = itemRegistry;
        _world = world;
        _logger = logger;
        _abilityRegistry = abilityRegistry;
        _commandRegistry = commandRegistry;
        _tagRegistry = tagRegistry;
        _manifestProvider = manifestProvider;
    }

    public void Validate()
    {
        var issueCount = 0;

        issueCount += ValidateMobs();
        issueCount += ValidateItems();
        issueCount += ValidateRooms();
        issueCount += ValidateTags();

        _logger.LogInformation("Pack validation complete: {Count} issue(s) found", issueCount);
    }

    private int ValidateMobs()
    {
        var count = 0;

        foreach (var template in _spawnManager.AllTemplates)
        {
            if (template.Tags.Contains("skill_trainer"))
            {
                if (!template.Properties.TryGetValue(TrainingProperties.TrainerConfigKey, out var trainerObj)
                    || trainerObj is not TrainerConfig trainerConfig)
                {
                    _logger.LogWarning(
                        "Mob {Id} has tag 'skill_trainer' but TrainerConfig is missing or malformed",
                        template.Id);
                    count++;
                    continue;
                }

                if (trainerConfig.AbilityIds.Count == 0)
                {
                    _logger.LogWarning(
                        "Mob {Id} has tag 'skill_trainer' but TrainerConfig.AbilityIds is empty",
                        template.Id);
                    count++;
                }
            }

            if (template.Tags.Contains(ShopProperties.ShopTag))
            {
                if (!template.Properties.ContainsKey(ShopProperties.Sells))
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

            if (battleCommands.Count > 0 && abilities.Count == 0)
            {
                _logger.LogWarning(
                    "Mob {Id} has battlecommands but no abilities -- commands will fizzle", template.Id);
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
            _ = template;
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
            .ToDictionary(m => m.Name, m => m, StringComparer.OrdinalIgnoreCase);

        foreach (var template in _spawnManager.AllTemplates)
        {
            count += ValidateEntityTags(template.Id, template.Tags, "mob", manifests);
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
            && manifest.TagValidation == "lenient";
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
}
