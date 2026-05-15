using System;
using System.Collections.Generic;
using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Quests;
using Xunit;

namespace Tapestry.Engine.Tests.Quests;

public class QuestMarkerServiceTests
{
    private static (QuestMarkerService sut, QuestStateRepository repo, QuestRegistry registry)
        Setup()
    {
        var repo = new QuestStateRepository();
        var registry = new QuestRegistry();
        var sut = new QuestMarkerService(repo, registry);
        return (sut, repo, registry);
    }

    private static QuestDefinition MakeGiverQuest(string questId, string giverTemplate, bool secret = false) => new()
    {
        Id = questId,
        Name = "Test Quest",
        Type = "side",
        Giver = giverTemplate,
        Secret = secret,
        Abandonable = true,
        Prereqs = new QuestPrereqs { MinLevel = 1 },
        Stages =
        [
            new QuestStage
            {
                Id = "stage-1",
                Objectives =
                [
                    new QuestObjective
                    {
                        Id = "obj-1",
                        Type = "kill",
                        Target = "test:trolloc",
                        Count = 1,
                    },
                ],
            },
        ],
        Rewards = new QuestReward { Xp = 50 },
    };

    private static QuestDefinition MakeDeliverQuest(string questId, string npcTemplate) => new()
    {
        Id = questId,
        Name = "Deliver Quest",
        Type = "side",
        Giver = "test:wisdom-npc",
        Abandonable = true,
        Prereqs = new QuestPrereqs { MinLevel = 1 },
        Stages =
        [
            new QuestStage
            {
                Id = "stage-deliver",
                Objectives =
                [
                    new QuestObjective
                    {
                        Id = "obj-deliver",
                        Type = "deliver",
                        Target = "test:letter",
                        Npc = npcTemplate,
                        Count = 1,
                    },
                ],
            },
        ],
        Rewards = new QuestReward { Xp = 75 },
    };

    private static Entity MakeNpc(string templateId)
    {
        var npc = new Entity("npc", "Test Npc");
        npc.SetProperty(CommonProperties.TemplateId, templateId);
        return npc;
    }

    [Fact]
    public void HasQuestMarker_ReturnsTrue_ForGiverNpcOfActiveNonSecretQuest()
    {
        var (sut, repo, registry) = Setup();
        var quest = MakeGiverQuest("test:wisdom-quest", "test:wisdom-npc");
        registry.RegisterForTest(quest);

        var playerId = Guid.NewGuid();
        var state = repo.GetOrCreate(playerId);
        state.Active.Add(new ActiveQuest { QuestId = quest.Id, StageIndex = 0 });

        sut.HasQuestMarker(playerId, "test:wisdom-npc").Should().BeTrue();
    }

    [Fact]
    public void HasQuestMarker_ReturnsFalse_ForGiverNpcOfSecretQuest()
    {
        var (sut, repo, registry) = Setup();
        var quest = MakeGiverQuest("test:wisdom-quest", "test:wisdom-npc", secret: true);
        registry.RegisterForTest(quest);

        var playerId = Guid.NewGuid();
        var state = repo.GetOrCreate(playerId);
        state.Active.Add(new ActiveQuest { QuestId = quest.Id, StageIndex = 0 });

        sut.HasQuestMarker(playerId, "test:wisdom-npc").Should().BeFalse();
    }

    [Fact]
    public void HasQuestMarker_ReturnsTrue_ForDeliverNpcOfActiveCurrentStage()
    {
        var (sut, repo, registry) = Setup();
        var quest = MakeDeliverQuest("test:deliver-quest", "test:lan-npc");
        registry.RegisterForTest(quest);

        var playerId = Guid.NewGuid();
        var state = repo.GetOrCreate(playerId);
        state.Active.Add(new ActiveQuest { QuestId = quest.Id, StageIndex = 0 });

        sut.HasQuestMarker(playerId, "test:lan-npc").Should().BeTrue();
    }

    [Fact]
    public void HasQuestMarker_ReturnsFalse_ForKillTargetTemplate()
    {
        var (sut, repo, registry) = Setup();
        var quest = MakeGiverQuest("test:kill-quest", "test:wisdom-npc");
        registry.RegisterForTest(quest);

        var playerId = Guid.NewGuid();
        var state = repo.GetOrCreate(playerId);
        state.Active.Add(new ActiveQuest { QuestId = quest.Id, StageIndex = 0 });

        // kill target "test:trolloc" should NOT get a quest marker
        sut.HasQuestMarker(playerId, "test:trolloc").Should().BeFalse();
    }

    [Fact]
    public void GetQuestMarkersForRoom_ReturnsEntitiesWithMarkers()
    {
        var (sut, repo, registry) = Setup();
        var quest = MakeGiverQuest("test:wisdom-quest", "test:wisdom-npc");
        registry.RegisterForTest(quest);

        var playerId = Guid.NewGuid();
        var state = repo.GetOrCreate(playerId);
        state.Active.Add(new ActiveQuest { QuestId = quest.Id, StageIndex = 0 });

        var questNpc = MakeNpc("test:wisdom-npc");
        var irrelevantNpc = MakeNpc("test:random-npc");

        var markers = sut.GetQuestMarkersForRoom(playerId, [questNpc, irrelevantNpc]);

        markers.Should().HaveCount(1);
        markers[0].EntityId.Should().Be(questNpc.Id);
        markers[0].TemplateId.Should().Be("test:wisdom-npc");
        markers[0].QuestId.Should().Be(quest.Id);
    }

    [Fact]
    public void GetQuestMarkersForRoom_ReturnsEmpty_WhenPlayerHasNoActiveQuests()
    {
        var (sut, repo, registry) = Setup();

        var playerId = Guid.NewGuid();
        var npc = MakeNpc("test:wisdom-npc");

        var markers = sut.GetQuestMarkersForRoom(playerId, [npc]);

        markers.Should().BeEmpty();
    }
}
