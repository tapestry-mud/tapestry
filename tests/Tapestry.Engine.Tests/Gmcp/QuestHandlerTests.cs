using FluentAssertions;
using Tapestry.Engine.Quests;
using Tapestry.Server.Gmcp.Handlers;
using Tapestry.Shared;
using Tapestry.Engine.Ui;

namespace Tapestry.Engine.Tests.Gmcp;

public class QuestHandlerTests
{
    private sealed record Harness(
        EventBus EventBus,
        NotificationQueue NotificationQueue,
        Guid PlayerId);

    private static Harness Build()
    {
        var eventBus = new EventBus();
        var notificationQueue = new NotificationQueue();
        var registry = new QuestRegistry();

        registry.RegisterForTest(new QuestDefinition
        {
            Id = "test:quest",
            Name = "A Dangerous Errand",
            Type = "side",
            Stages =
            [
                new QuestStage
                {
                    Id = "first",
                    Description = "Defeat the creatures outside the village.",
                    Objectives =
                    [
                        new QuestObjective
                        {
                            Id = "defeat-creatures",
                            Type = "kill",
                            Target = "test:creature",
                            Count = 3,
                            Description = "Defeat three creatures",
                        },
                    ],
                },
                new QuestStage
                {
                    Id = "return",
                    Description = "Return to the village elder.",
                    Objectives =
                    [
                        new QuestObjective
                        {
                            Id = "return-to-elder",
                            Type = "visit",
                            Target = "test:village",
                            Count = 1,
                            Description = "Return to the elder",
                        },
                    ],
                },
            ],
            Rewards = new QuestReward
            {
                Xp = 100,
                Gold = 25,
            },
        });

        var questService = new QuestService(
            registry,
            new QuestStateRepository(),
            eventBus,
            new QuestConfig(),
            null,
            new NoopRewardDispatcher(),
            new NoopPersistence());

        var handler = new QuestHandler(
            new FakeGmcpConnectionManager(),
            questService,
            registry,
            eventBus,
            notificationQueue,
            new PanelRenderer());

        handler.Configure();

        return new Harness(eventBus, notificationQueue, Guid.NewGuid());
    }

    [Fact]
    public void StageAdvanced_NotificationUsesPanelRenderer()
    {
        var harness = Build();

        harness.EventBus.Publish(new GameEvent
        {
            Type = "quest.stage.advanced",
            SourceEntityId = harness.PlayerId,
            Data =
            {
                ["questId"] = "test:quest",
                ["stageIndex"] = 1,
            },
        });

        var notifications =
            harness.NotificationQueue.DrainFor(harness.PlayerId);

        notifications.Should().ContainSingle();

        var notification = notifications[0];
        notification.Type.Should().Be("quest_stage");
        notification.Text.Should().Contain("<frame>|");
        notification.Text.Should().Contain("<title>");
        notification.Text.Should().Contain("A Dangerous Errand");
        notification.Text.Should().Contain("Return to the village elder.");
        notification.Text.Should().NotContain("[Quest:");
    }

    [Fact]
    public void Completed_NotificationUsesPanelRenderer()
    {
        var harness = Build();

        harness.EventBus.Publish(new GameEvent
        {
            Type = "quest.completed",
            SourceEntityId = harness.PlayerId,
            Data =
            {
                ["questId"] = "test:quest",
                ["rewardXp"] = 100,
                ["rewardGold"] = 25,
                ["classUnlock"] = null,
            },
        });

        var notifications =
            harness.NotificationQueue.DrainFor(harness.PlayerId);

        notifications.Should().ContainSingle();

        var notification = notifications[0];
        notification.Type.Should().Be("quest_complete");
        notification.Text.Should().Contain("<frame>|");
        notification.Text.Should().Contain("<title>");
        notification.Text.Should().Contain("A Dangerous Errand");
        notification.Text.Should().Contain("100 XP");
        notification.Text.Should().Contain("25 gold");
        notification.Text.Should().NotContain("[Quest Complete]");
    }

    private sealed class NoopRewardDispatcher : IQuestRewardDispatcher
    {
        public void Dispatch(Entity player, QuestReward? rewards)
        {
        }
    }

    private sealed class NoopPersistence : IQuestPersistence
    {
        public void Save(string playerName, QuestState state)
        {
        }
    }
}
