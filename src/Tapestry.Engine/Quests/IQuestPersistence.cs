namespace Tapestry.Engine.Quests;

public interface IQuestPersistence
{
    void Save(string playerName, QuestState state);
}
