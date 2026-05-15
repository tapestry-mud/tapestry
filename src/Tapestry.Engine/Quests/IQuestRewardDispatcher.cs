namespace Tapestry.Engine.Quests;

public interface IQuestRewardDispatcher
{
    void Dispatch(Entity player, QuestReward? rewards);
}
