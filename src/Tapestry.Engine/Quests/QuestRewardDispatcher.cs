namespace Tapestry.Engine.Quests;

/// <summary>
/// Auto-applies all rewards from a QuestReward to a player entity on quest completion.
/// </summary>
public class QuestRewardDispatcher
{
    private readonly IQuestProgressionService _progression;
    private readonly IQuestCurrencyService _currency;
    private readonly IQuestProficiencyService _proficiency;
    private readonly IQuestItemRegistry _items;
    private readonly IQuestInventoryService _inventory;

    public QuestRewardDispatcher(
        IQuestProgressionService progression,
        IQuestCurrencyService currency,
        IQuestProficiencyService proficiency,
        IQuestItemRegistry items,
        IQuestInventoryService inventory)
    {
        _progression = progression;
        _currency = currency;
        _proficiency = proficiency;
        _items = items;
        _inventory = inventory;
    }

    public void Dispatch(Entity player, QuestReward? rewards)
    {
        if (rewards == null) { return; }

        if (rewards.Xp > 0)
        {
            _progression.GrantExperience(player.Id, rewards.Xp, "main", "quest");
        }

        if (rewards.Gold > 0)
        {
            _currency.AddGold(player, rewards.Gold, "quest_reward");
        }

        foreach (var abilityId in rewards.Abilities)
        {
            _proficiency.Learn(player.Id, abilityId, initialProficiency: 1);
        }

        if (!string.IsNullOrEmpty(rewards.ClassUnlock))
        {
            player.SetProperty("class", rewards.ClassUnlock);
        }

        if (!string.IsNullOrEmpty(rewards.RaceUnlock))
        {
            player.SetProperty("race", rewards.RaceUnlock);
        }

        foreach (var templateId in rewards.Items)
        {
            var item = _items.CreateItem(templateId);
            if (item == null)
            {
                continue;
            }
            _inventory.PickUp(player, item, silent: true);
        }
    }
}
