using Tapestry.Engine.Quests;
using FluentAssertions;

public class QuestRegistryTests
{
    private const string TestQuestYaml = """
        id: "test:simple-quest"
        name: "Simple Quest"
        type: side
        abandonable: true
        prereqs:
          min_level: 1
        stages:
          - id: "kill"
            description: "Kill something"
            objectives:
              - id: "kill-0"
                type: kill
                target: "test:mob"
                count: 3
                description: "Kill 3 mobs"
        rewards:
          xp: 100
          gold: 0
        """;

    [Fact]
    public void Load_ParsesQuestFromYaml()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var questDir = Path.Combine(dir, "quests");
        Directory.CreateDirectory(questDir);
        File.WriteAllText(Path.Combine(questDir, "simple-quest.yaml"), TestQuestYaml);

        var registry = new QuestRegistry();
        registry.Load([dir]);

        var quest = registry.Get("test:simple-quest");
        quest.Should().NotBeNull();
        quest!.Name.Should().Be("Simple Quest");
        quest.Stages.Should().HaveCount(1);
        quest.Stages[0].Objectives.Should().HaveCount(1);
        quest.Stages[0].Objectives[0].Count.Should().Be(3);
        quest.Prereqs.MinLevel.Should().Be(1);
        quest.Rewards.Xp.Should().Be(100);
    }

    [Fact]
    public void Load_SkipsPacksWithNoQuestDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);

        var registry = new QuestRegistry();
        registry.Load([dir]);

        registry.All().Should().BeEmpty();
    }

    [Fact]
    public void Get_ReturnsNull_ForUnknownId()
    {
        var registry = new QuestRegistry();
        registry.Load([]);

        registry.Get("nonexistent:quest").Should().BeNull();
    }

    [Fact]
    public void Load_AutoGeneratesObjectiveIds_WhenMissing()
    {
        var yaml = """
            id: "test:no-ids"
            name: "No IDs"
            type: side
            stages:
              - id: "stage-0"
                objectives:
                  - type: kill
                    target: "test:mob"
                    count: 1
                    description: "Kill"
            rewards:
              xp: 0
            """;

        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var questDir = Path.Combine(dir, "quests");
        Directory.CreateDirectory(questDir);
        File.WriteAllText(Path.Combine(questDir, "no-ids.yaml"), yaml);

        var registry = new QuestRegistry();
        registry.Load([dir]);

        var quest = registry.Get("test:no-ids");
        quest!.Stages[0].Objectives[0].Id.Should().NotBeNullOrEmpty();
    }
}
