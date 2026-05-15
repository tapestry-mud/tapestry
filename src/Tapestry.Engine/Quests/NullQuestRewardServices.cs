namespace Tapestry.Engine.Quests;

/// <summary>
/// No-op reward services used when the real implementations are not registered.
/// Packs override these via their own DI registrations.
/// </summary>
internal sealed class NullQuestProgressionService : IQuestProgressionService
{
    public void GrantExperience(Guid entityId, int amount, string trackName, string source) { }
}

internal sealed class NullQuestCurrencyService : IQuestCurrencyService
{
    public int AddGold(Entity entity, int delta, string reason) => 0;
}

internal sealed class NullQuestProficiencyService : IQuestProficiencyService
{
    public void Learn(Guid entityId, string abilityId, int initialProficiency = 1) { }
}

internal sealed class NullQuestItemRegistry : IQuestItemRegistry
{
    public Entity? CreateItem(string templateId) => null;
}

internal sealed class NullQuestInventoryService : IQuestInventoryService
{
    public bool PickUp(Entity entity, Entity item, bool silent = false) => false;
}

internal sealed class NullQuestPersistence : IQuestPersistence
{
    public void Save(string playerName, QuestState state) { }
}
