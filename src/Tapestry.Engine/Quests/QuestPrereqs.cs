using YamlDotNet.Serialization;

namespace Tapestry.Engine.Quests;

public class QuestPrereqs
{
    [YamlMember(Alias = "min_level")]
    public int MinLevel { get; set; }

    [YamlMember(Alias = "class")]
    public string? Class { get; set; }

    [YamlMember(Alias = "quests_completed")]
    public List<string> QuestsCompleted { get; set; } = new();

    [YamlMember(Alias = "quests_not_completed")]
    public List<string> QuestsNotCompleted { get; set; } = new();
}
