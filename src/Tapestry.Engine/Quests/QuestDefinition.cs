using YamlDotNet.Serialization;

namespace Tapestry.Engine.Quests;

public class QuestDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "side";
    public string? Giver { get; set; }
    public bool Repeatable { get; set; }
    public bool Abandonable { get; set; } = true;
    public QuestPrereqs Prereqs { get; set; } = new();
    public List<QuestStage> Stages { get; set; } = new();
    public QuestReward Rewards { get; set; } = new();
}
