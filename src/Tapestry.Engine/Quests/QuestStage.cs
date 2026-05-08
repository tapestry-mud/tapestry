namespace Tapestry.Engine.Quests;

public class QuestStage
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public List<QuestObjective> Objectives { get; set; } = new();
}
