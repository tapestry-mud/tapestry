using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Training;

namespace Tapestry.Scripting;

public class PackValidator
{
    private readonly SpawnManager _spawnManager;
    private readonly ItemRegistry _itemRegistry;
    private readonly World _world;
    private readonly ILogger<PackValidator> _logger;

    public PackValidator(
        SpawnManager spawnManager,
        ItemRegistry itemRegistry,
        World world,
        ILogger<PackValidator> logger)
    {
        _spawnManager = spawnManager;
        _itemRegistry = itemRegistry;
        _world = world;
        _logger = logger;
    }

    public void Validate()
    {
        var issueCount = 0;

        issueCount += ValidateMobs();
        issueCount += ValidateItems();
        issueCount += ValidateRooms();

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
}
