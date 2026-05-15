using YamlDotNet.Serialization;

namespace Tapestry.Engine.Quests;

public class ActiveQuest
{
    [YamlMember(Alias = "quest_id")]
    public string QuestId { get; set; } = "";

    [YamlMember(Alias = "stage_index")]
    public int StageIndex { get; set; }

    public List<QuestObjectiveState> Objectives { get; set; } = [];
}
