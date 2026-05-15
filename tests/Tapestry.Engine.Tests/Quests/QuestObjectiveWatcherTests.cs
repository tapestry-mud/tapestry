using Tapestry.Engine;
using Tapestry.Engine.Items;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Quests;
using Tapestry.Shared;
using FluentAssertions;

public class QuestObjectiveWatcherTests
{
    private static (QuestObjectiveWatcher watcher, QuestService service, EventBus bus,
        QuestStateRepository repo, Entity player) Setup(
        QuestDefinition quest,
        World? world = null,
        ItemRegistry? items = null,
        IEnumerable<QuestDefinition>? extraQuests = null)
    {
        var bus = new EventBus();
        var repo = new QuestStateRepository();
        var registry = new QuestRegistry();
        registry.RegisterForTest(quest);
        if (extraQuests != null)
        {
            foreach (var extra in extraQuests)
            {
                registry.RegisterForTest(extra);
            }
        }
        var config = new QuestConfig { ActiveCap = 10 };
        var service = new QuestService(registry, repo, bus, config,
            null, new FakeQuestRewardDispatcher(), new FakePersistence());
        world ??= new World();
        var watcher = new QuestObjectiveWatcher(service, repo, bus, world, items);
        watcher.Start();

        var player = new Entity("player", "Alice");
        player.SetProperty(ProgressionProperties.Level, new Dictionary<string, int> { ["main"] = 1 });
        world.TrackEntity(player);
        service.AcceptQuest(player, quest.Id);

        return (watcher, service, bus, repo, player);
    }

    private static QuestDefinition MakeKillQuest() => new()
    {
        Id = "test:kill-quest",
        Name = "Kill Quest",
        Type = "side",
        Abandonable = true,
        Prereqs = new QuestPrereqs { MinLevel = 1 },
        Stages =
        [
            new QuestStage
            {
                Id = "stage-kill",
                Objectives =
                [
                    new QuestObjective
                    {
                        Id = "kill-trollocs",
                        Type = "kill",
                        Target = "lf:trolloc",
                        Count = 2,
                    },
                ],
            },
        ],
        Rewards = new QuestReward { Xp = 100 },
    };

    private static QuestDefinition MakeCollectQuest() => new()
    {
        Id = "test:collect-quest",
        Name = "Collect Quest",
        Type = "side",
        Abandonable = true,
        Prereqs = new QuestPrereqs { MinLevel = 1 },
        Stages =
        [
            new QuestStage
            {
                Id = "stage-collect",
                Objectives =
                [
                    new QuestObjective
                    {
                        Id = "collect-angreal",
                        Type = "collect",
                        Target = "lf:angreal",
                        Count = 1,
                    },
                ],
            },
        ],
        Rewards = new QuestReward { Xp = 50 },
    };

    private static QuestDefinition MakeDeliverQuest() => new()
    {
        Id = "test:deliver-quest",
        Name = "Deliver Quest",
        Type = "side",
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
                        Id = "deliver-letter",
                        Type = "deliver",
                        Target = "lf:letter",
                        Npc = "lf:lan",
                        Count = 1,
                    },
                ],
            },
        ],
        Rewards = new QuestReward { Xp = 75 },
    };

    private static QuestDefinition MakeVisitQuest() => new()
    {
        Id = "test:visit-quest",
        Name = "Visit Quest",
        Type = "side",
        Abandonable = true,
        Prereqs = new QuestPrereqs { MinLevel = 1 },
        Stages =
        [
            new QuestStage
            {
                Id = "stage-visit",
                Objectives =
                [
                    new QuestObjective
                    {
                        Id = "visit-fal-dara",
                        Type = "visit",
                        Target = "lf:room:fal-dara",
                        Count = 1,
                    },
                ],
            },
        ],
        Rewards = new QuestReward { Xp = 25 },
    };

    [Fact]
    public void MobKilled_AdvancesKillObjective_WhenTemplateMatches()
    {
        var (_, _, bus, repo, player) = Setup(MakeKillQuest());

        bus.Publish(new GameEvent
        {
            Type = "mob.killed",
            SourceEntityId = player.Id,
            Data = { ["templateId"] = "lf:trolloc" },
        });

        var obj = repo.GetOrCreate(player.Id).Active[0].Objectives[0];
        obj.Current.Should().Be(1);
    }

    [Fact]
    public void MobKilled_DoesNotAdvance_WhenTemplateMismatch()
    {
        var (_, _, bus, repo, player) = Setup(MakeKillQuest());

        bus.Publish(new GameEvent
        {
            Type = "mob.killed",
            SourceEntityId = player.Id,
            Data = { ["templateId"] = "lf:myrddraal" },
        });

        var obj = repo.GetOrCreate(player.Id).Active[0].Objectives[0];
        obj.Current.Should().Be(0);
    }

    [Fact]
    public void ItemPickedUp_AdvancesCollectObjective()
    {
        var (_, service, bus, repo, player) = Setup(MakeCollectQuest());

        bus.Publish(new GameEvent
        {
            Type = "entity.item.picked_up",
            SourceEntityId = player.Id,
            Data = { ["templateId"] = "lf:angreal" },
        });

        // Count = 1, so one pick-up completes the objective and finishes the quest.
        service.IsCompleted(player.Id, "test:collect-quest").Should().BeTrue();
    }

    [Fact]
    public void ItemGiven_AdvancesDeliverObjective_WhenNpcMatches()
    {
        var world = new World();

        var npc = new Entity("npc", "Lan Mandragoran");
        npc.SetProperty(CommonProperties.TemplateId, "lf:lan");
        world.TrackEntity(npc);

        var (_, service, bus, _, player) = Setup(MakeDeliverQuest(), world);

        bus.Publish(new GameEvent
        {
            Type = "entity.item.given",
            SourceEntityId = player.Id,
            TargetEntityId = npc.Id,
            Data = { ["templateId"] = "lf:letter" },
        });

        // Count = 1, so one delivery completes the objective and finishes the quest.
        service.IsCompleted(player.Id, "test:deliver-quest").Should().BeTrue();
    }

    [Fact]
    public void ItemGiven_DoesNotAdvance_WhenNpcTemplateDoesNotMatch()
    {
        var world = new World();

        var npc = new Entity("npc", "Wrong NPC");
        npc.SetProperty(CommonProperties.TemplateId, "lf:moiraine");
        world.TrackEntity(npc);

        var (_, _, bus, repo, player) = Setup(MakeDeliverQuest(), world);

        bus.Publish(new GameEvent
        {
            Type = "entity.item.given",
            SourceEntityId = player.Id,
            TargetEntityId = npc.Id,
            Data = { ["templateId"] = "lf:letter" },
        });

        var obj = repo.GetOrCreate(player.Id).Active[0].Objectives[0];
        obj.Current.Should().Be(0);
    }

    [Fact]
    public void PlayerMoved_AdvancesVisitObjective_WhenRoomMatches()
    {
        var (_, service, bus, _, player) = Setup(MakeVisitQuest());

        bus.Publish(new GameEvent
        {
            Type = "player.moved",
            SourceEntityId = player.Id,
            Data = { ["new_room_id"] = "lf:room:fal-dara" },
        });

        // Count = 1, so one room visit completes the objective and finishes the quest.
        service.IsCompleted(player.Id, "test:visit-quest").Should().BeTrue();
    }

    [Fact]
    public void PlayerMoved_GrantsQuest_WhenRoomHasQuestGrantProperty()
    {
        var grantedQuest = new QuestDefinition
        {
            Id = "test:room-granted-quest",
            Name = "Room Granted Quest",
            Type = "side",
            Abandonable = true,
            Prereqs = new QuestPrereqs { MinLevel = 1 },
            Stages =
            [
                new QuestStage
                {
                    Id = "stage-granted",
                    Objectives =
                    [
                        new QuestObjective
                        {
                            Id = "kill-goblin",
                            Type = "kill",
                            Target = "lf:goblin",
                            Count = 1,
                        },
                    ],
                },
            ],
            Rewards = new QuestReward { Xp = 50 },
        };

        var world = new World();
        var room = new Room("lf:room:goblin-cave", "Goblin Cave", "A damp cave.");
        room.SetProperty("quest_grant", grantedQuest.Id);
        world.AddRoom(room);

        var (_, service, bus, repo, player) = Setup(MakeKillQuest(), world, extraQuests: [grantedQuest]);

        bus.Publish(new GameEvent
        {
            Type = "player.moved",
            SourceEntityId = player.Id,
            Data = { ["new_room_id"] = "lf:room:goblin-cave" },
        });

        repo.GetOrCreate(player.Id).Active.Should().Contain(a => a.QuestId == grantedQuest.Id);
    }

    [Fact]
    public void ItemPickedUp_AdvancesObjective_WhenEventDataContainsQuestAdvance()
    {
        var advanceQuest = new QuestDefinition
        {
            Id = "test:advance-quest",
            Name = "Advance Quest",
            Type = "side",
            Abandonable = true,
            Prereqs = new QuestPrereqs { MinLevel = 1 },
            Stages =
            [
                new QuestStage
                {
                    Id = "stage-advance",
                    Objectives =
                    [
                        new QuestObjective
                        {
                            Id = "deliver-blade",
                            Type = "deliver",
                            Target = "test:heron-blade",
                            Npc = "lf:warder",
                            Count = 3,
                        },
                    ],
                },
            ],
            Rewards = new QuestReward { Xp = 100 },
        };

        var (_, _, bus, repo, player) = Setup(advanceQuest);

        bus.Publish(new GameEvent
        {
            Type = "entity.item.picked_up",
            SourceEntityId = player.Id,
            Data =
            {
                ["templateId"] = "test:heron-blade",
                ["quest_advance"] = "test:advance-quest:deliver-blade",
            },
        });

        var obj = repo.GetOrCreate(player.Id).Active[0].Objectives[0];
        obj.Current.Should().Be(1);
    }
}
