namespace Tapestry.Engine.Quests;

public class QuestState
{
    public List<ActiveQuest> Active { get; set; } = new();
    public List<string> Completed { get; set; } = new();
}
