namespace Tapestry.Engine.Quests;

public class QuestObjective
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Target { get; set; } = "";
    public string? Npc { get; set; }
    public int Count { get; set; } = 1;
    public string Description { get; set; } = "";
}
