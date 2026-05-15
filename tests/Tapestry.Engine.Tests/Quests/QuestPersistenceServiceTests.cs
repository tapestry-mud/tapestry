using System;
using System.IO;
using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Quests;
using Tapestry.Data;
using Xunit;

namespace Tapestry.Engine.Tests.Quests;

public class QuestPersistenceServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _playersDir;
    private readonly QuestPersistenceService _sut;
    private readonly QuestRegistry _registry;

    public QuestPersistenceServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _playersDir = Path.Combine(_tempDir, "players");
        Directory.CreateDirectory(_playersDir);

        _registry = new QuestRegistry();
        var config = new ServerConfig
        {
            Persistence = new PersistenceSection { SavePath = _tempDir }
        };
        _sut = new QuestPersistenceService(config, _registry, new EventBus(), new QuestStateRepository());
    }

    [Fact]
    public void Save_WritesYamlSidecarFile()
    {
        var state = new QuestState
        {
            Active =
            [
                new ActiveQuest
                {
                    QuestId = "test:quest-a",
                    StageIndex = 1,
                    Objectives =
                    [
                        new QuestObjectiveState { ObjectiveId = "kill-trollocs", Current = 4, Required = 10 }
                    ]
                }
            ],
            Completed = ["test:quest-b"]
        };

        _sut.Save("Alice", state);

        var path = Path.Combine(_playersDir, "alice.quests.yaml");
        File.Exists(path).Should().BeTrue();
        var content = File.ReadAllText(path);
        content.Should().Contain("quest_id: test:quest-a");
        content.Should().Contain("stage_index: 1");
        content.Should().Contain("current: 4");
        content.Should().Contain("required: 10");
    }

    [Fact]
    public void Load_ReturnsNull_WhenFileDoesNotExist()
    {
        var result = _sut.Load("NewPlayer");
        result.Should().BeNull();
    }

    [Fact]
    public void Load_RoundTrips_QuestState()
    {
        var state = new QuestState
        {
            Active =
            [
                new ActiveQuest
                {
                    QuestId = "test:quest-a",
                    StageIndex = 0,
                    Objectives =
                    [
                        new QuestObjectiveState { ObjectiveId = "obj-1", Current = 3, Required = 5 }
                    ]
                }
            ],
            Completed = ["test:old-quest"]
        };
        _sut.Save("Bob", state);

        var loaded = _sut.Load("Bob");

        loaded.Should().NotBeNull();
        loaded!.Active.Should().HaveCount(1);
        loaded.Active[0].QuestId.Should().Be("test:quest-a");
        loaded.Active[0].StageIndex.Should().Be(0);
        loaded.Active[0].Objectives[0].ObjectiveId.Should().Be("obj-1");
        loaded.Active[0].Objectives[0].Current.Should().Be(3);
        loaded.Active[0].Objectives[0].Required.Should().Be(5);
        loaded.Completed.Should().Contain("test:old-quest");
    }

    [Fact]
    public void Load_RemovesOrphanedQuests_WhenQuestNoLongerExistsInRegistry()
    {
        var state = new QuestState
        {
            Active =
            [
                new ActiveQuest { QuestId = "test:known-quest", StageIndex = 0, Objectives = [] },
                new ActiveQuest { QuestId = "test:removed-quest", StageIndex = 0, Objectives = [] }
            ],
            Completed = ["test:removed-quest-2", "test:also-known"]
        };
        _registry.RegisterForTest(new QuestDefinition { Id = "test:known-quest", Name = "Known" });
        _registry.RegisterForTest(new QuestDefinition { Id = "test:also-known", Name = "Also Known" });
        _sut.Save("Charlie", state);

        var loaded = _sut.Load("Charlie");

        loaded!.Active.Should().HaveCount(1);
        loaded.Active[0].QuestId.Should().Be("test:known-quest");
        loaded.Completed.Should().Contain("test:also-known");
        loaded.Completed.Should().NotContain("test:removed-quest-2");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);
}
