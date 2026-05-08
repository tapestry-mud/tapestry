namespace Tapestry.Engine.Quests;

public class ActiveQuest
{
    public string QuestId { get; set; } = "";
    public int StageIndex { get; set; }
    public List<QuestObjectiveState> Objectives { get; set; } = new();
}
