using YamlDotNet.Serialization;

namespace Tapestry.Engine.Quests;

public class QuestReward
{
    public int Xp { get; set; }
    public int Gold { get; set; }
    public List<string> Items { get; set; } = new();
    public List<string> Abilities { get; set; } = new();

    [YamlMember(Alias = "class_unlock")]
    public string? ClassUnlock { get; set; }

    [YamlMember(Alias = "race_unlock")]
    public string? RaceUnlock { get; set; }
}
