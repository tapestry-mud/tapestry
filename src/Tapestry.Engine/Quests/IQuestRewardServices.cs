namespace Tapestry.Engine.Quests;

/// <summary>
/// Grants experience on quest completion.
/// </summary>
public interface IQuestProgressionService
{
    void GrantExperience(Guid entityId, int amount, string trackName, string source);
}

/// <summary>
/// Awards gold on quest completion.
/// </summary>
public interface IQuestCurrencyService
{
    int AddGold(Entity entity, int delta, string reason);
}

/// <summary>
/// Teaches abilities on quest completion.
/// </summary>
public interface IQuestProficiencyService
{
    void Learn(Guid entityId, string abilityId, int initialProficiency = 1);
}

/// <summary>
/// Creates item instances on quest completion.
/// </summary>
public interface IQuestItemRegistry
{
    Entity? CreateItem(string templateId);
}

/// <summary>
/// Places items into a player's inventory on quest completion.
/// </summary>
public interface IQuestInventoryService
{
    bool PickUp(Entity entity, Entity item, bool silent = false);
}
