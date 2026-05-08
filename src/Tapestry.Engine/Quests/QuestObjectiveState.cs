namespace Tapestry.Engine.Quests;

public class QuestObjectiveState
{
    public string ObjectiveId { get; set; } = "";
    public int Current { get; set; }
    public int Required { get; set; }
    public bool Complete => Current >= Required;
}
