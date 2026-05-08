using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Engine.Quests;

public class QuestRegistry
{
    private readonly Dictionary<string, QuestDefinition> _quests = new();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public void Load(IEnumerable<string> packDirectories)
    {
        foreach (var packDir in packDirectories)
        {
            var questDir = Path.Combine(packDir, "quests");
            if (!Directory.Exists(questDir)) { continue; }

            foreach (var file in Directory.GetFiles(questDir, "*.yaml"))
            {
                var yaml = File.ReadAllText(file);
                var quest = Deserializer.Deserialize<QuestDefinition>(yaml);
                EnsureObjectiveIds(quest);
                _quests[quest.Id] = quest;
            }
        }
    }

    public QuestDefinition? Get(string questId) =>
        _quests.GetValueOrDefault(questId);

    public IReadOnlyCollection<QuestDefinition> All() =>
        _quests.Values;

    internal void RegisterForTest(QuestDefinition quest) => _quests[quest.Id] = quest;

    private static void EnsureObjectiveIds(QuestDefinition quest)
    {
        for (var si = 0; si < quest.Stages.Count; si++)
        {
            var stage = quest.Stages[si];
            for (var oi = 0; oi < stage.Objectives.Count; oi++)
            {
                var obj = stage.Objectives[oi];
                if (string.IsNullOrEmpty(obj.Id))
                {
                    obj.Id = $"{stage.Id}-{obj.Type}-{oi}";
                }
            }
        }
    }
}
