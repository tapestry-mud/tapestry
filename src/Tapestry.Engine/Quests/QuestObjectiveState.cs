using YamlDotNet.Serialization;

namespace Tapestry.Engine.Quests;

public class QuestObjectiveState
{
    [YamlMember(Alias = "objective_id")]
    public string ObjectiveId { get; set; } = "";

    public int Current { get; set; }

    public int Required { get; set; }

    [YamlIgnore]
    public bool Complete => Current >= Required;
}
