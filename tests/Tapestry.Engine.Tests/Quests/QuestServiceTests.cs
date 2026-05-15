using Tapestry.Engine;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Quests;
using Tapestry.Shared;
using FluentAssertions;

public class QuestServiceTests
{
    private static (QuestService service, QuestStateRepository repo, EventBus bus, Entity player)
        Setup(QuestDefinition quest)
    {
        var (service, repo, bus, player, _, _, _) = SetupFull(quest);
        return (service, repo, bus, player);
    }

    private static (QuestService service, QuestStateRepository repo, EventBus bus, Entity player,
        FakeQuestScriptLoader scriptLoader, FakeQuestRewardDispatcher rewards, FakePersistence persistence)
        SetupFull(QuestDefinition quest)
    {
        var bus = new EventBus();
        var repo = new QuestStateRepository();
        var registry = new QuestRegistry();
        registry.RegisterForTest(quest);
        var config = new QuestConfig { ActiveCap = 10 };
        var scriptLoader = new FakeQuestScriptLoader();
        var rewards = new FakeQuestRewardDispatcher();
        var persistence = new FakePersistence();
        var service = new QuestService(registry, repo, bus, config,
            scriptLoader, rewards, persistence);
        var player = new Entity("player", "Alice");
        player.SetProperty(ProgressionProperties.Level, new Dictionary<string, int> { ["main"] = 1 });
        return (service, repo, bus, player, scriptLoader, rewards, persistence);
    }

    private static QuestDefinition MakeKillQuest(string id = "test:kill-quest") => new()
    {
        Id = id,
        Name = "Kill Quest",
        Type = "side",
        Abandonable = true,
        Prereqs = new QuestPrereqs { MinLevel = 1 },
        Stages =
        [
            new QuestStage
            {
                Id = "kill",
                Objectives =
                [
                    new QuestObjective
                    {
                        Id = "kill-trollocs",
                        Type = "kill",
                        Target = "lf:trolloc",
                        Count = 3,
                    },
                ],
            },
        ],
        Rewards = new QuestReward { Xp = 100, Gold = 10 },
    };

    [Fact]
    public void AcceptQuest_AddsToActiveList()
    {
        var (service, repo, _, player) = Setup(MakeKillQuest());
        service.AcceptQuest(player, "test:kill-quest");
        var state = repo.GetOrCreate(player.Id);
        state.Active.Should().ContainSingle(q => q.QuestId == "test:kill-quest");
    }

    [Fact]
    public void AcceptQuest_InitializesObjectiveStates()
    {
        var (service, repo, _, player) = Setup(MakeKillQuest());
        service.AcceptQuest(player, "test:kill-quest");
        var active = repo.GetOrCreate(player.Id).Active[0];
        active.Objectives.Should().ContainSingle(o =>
            o.ObjectiveId == "kill-trollocs" && o.Required == 3 && o.Current == 0);
    }

    [Fact]
    public void AcceptQuest_RejectsWhenAlreadyActive()
    {
        var (service, _, _, player) = Setup(MakeKillQuest());
        service.AcceptQuest(player, "test:kill-quest");
        var result = service.AcceptQuest(player, "test:kill-quest");
        result.Should().Be(AcceptQuestResult.AlreadyActive);
    }

    [Fact]
    public void AcceptQuest_RejectsWhenLevelTooLow()
    {
        var quest = MakeKillQuest();
        quest.Prereqs.MinLevel = 10;
        var (service, _, _, player) = Setup(quest);
        // Player level is 1 (set in Setup), too low for MinLevel 10
        var result = service.AcceptQuest(player, "test:kill-quest");
        result.Should().Be(AcceptQuestResult.PrereqNotMet);
    }

    [Fact]
    public void AcceptQuest_RejectsWhenCapReached()
    {
        var quest = MakeKillQuest();
        var config = new QuestConfig { ActiveCap = 1 };
        var bus = new EventBus();
        var repo = new QuestStateRepository();
        var registry = new QuestRegistry();
        registry.RegisterForTest(quest);
        var otherQuest = MakeKillQuest("test:other-quest");
        registry.RegisterForTest(otherQuest);
        var service = new QuestService(registry, repo, bus, config,
            null, new FakeQuestRewardDispatcher(), new FakePersistence());
        var player = new Entity("player", "Alice");
        player.SetProperty(ProgressionProperties.Level, new Dictionary<string, int> { ["main"] = 1 });

        service.AcceptQuest(player, "test:kill-quest");
        var result = service.AcceptQuest(player, "test:other-quest");
        result.Should().Be(AcceptQuestResult.CapReached);
    }

    [Fact]
    public void AcceptQuest_NonAbandonable_BypassesCap()
    {
        // Non-abandonable quests bypass the cap entirely
        var abandonableQuest = MakeKillQuest("test:side-quest");
        var mainQuest = MakeKillQuest("test:main-quest");
        mainQuest.Abandonable = false;

        var config = new QuestConfig { ActiveCap = 1 };
        var bus = new EventBus();
        var repo = new QuestStateRepository();
        var registry = new QuestRegistry();
        registry.RegisterForTest(abandonableQuest);
        registry.RegisterForTest(mainQuest);
        var service = new QuestService(registry, repo, bus, config,
            null, new FakeQuestRewardDispatcher(), new FakePersistence());
        var player = new Entity("player", "Alice");
        player.SetProperty(ProgressionProperties.Level, new Dictionary<string, int> { ["main"] = 1 });

        service.AcceptQuest(player, "test:side-quest"); // fills the cap
        var result = service.AcceptQuest(player, "test:main-quest"); // non-abandonable, should bypass
        result.Should().Be(AcceptQuestResult.Accepted);
    }

    [Fact]
    public void AdvanceObjective_IncrementsCounter()
    {
        var (service, repo, _, player) = Setup(MakeKillQuest());
        service.AcceptQuest(player, "test:kill-quest");
        service.AdvanceObjective(player.Id, "test:kill-quest", "kill-trollocs", 1);
        var obj = repo.GetOrCreate(player.Id).Active[0].Objectives[0];
        obj.Current.Should().Be(1);
        obj.Complete.Should().BeFalse();
    }

    [Fact]
    public void AdvanceObjective_CompletesQuest_WhenAllObjectivesMet()
    {
        var (service, repo, bus, player) = Setup(MakeKillQuest());
        service.AcceptQuest(player, "test:kill-quest");

        GameEvent? completedEvent = null;
        bus.Subscribe("quest.completed", evt => { completedEvent = evt; });

        service.AdvanceObjective(player.Id, "test:kill-quest", "kill-trollocs", 3);

        var state = repo.GetOrCreate(player.Id);
        state.Active.Should().BeEmpty();
        state.Completed.Should().Contain("test:kill-quest");
        completedEvent.Should().NotBeNull();
        completedEvent!.Data["questId"].Should().Be("test:kill-quest");
    }

    [Fact]
    public void AdvanceObjective_AdvancesStage_WhenAllObjectivesInStageMet()
    {
        var quest = new QuestDefinition
        {
            Id = "test:two-stage",
            Name = "Two Stage",
            Type = "side",
            Abandonable = true,
            Prereqs = new QuestPrereqs { MinLevel = 1 },
            Stages =
            [
                new QuestStage
                {
                    Id = "stage-0",
                    Objectives =
                    [
                        new QuestObjective { Id = "obj-0", Type = "kill", Target = "lf:mob", Count = 1 },
                    ],
                },
                new QuestStage
                {
                    Id = "stage-1",
                    Objectives =
                    [
                        new QuestObjective { Id = "obj-1", Type = "visit", Target = "lf:room", Count = 1 },
                    ],
                },
            ],
            Rewards = new QuestReward { Xp = 50 },
        };

        var bus = new EventBus();
        var repo = new QuestStateRepository();
        var registry = new QuestRegistry();
        registry.RegisterForTest(quest);
        var service = new QuestService(registry, repo, bus, new QuestConfig(),
            null, new FakeQuestRewardDispatcher(), new FakePersistence());
        var player = new Entity("player", "Alice");
        player.SetProperty(ProgressionProperties.Level, new Dictionary<string, int> { ["main"] = 1 });

        service.AcceptQuest(player, "test:two-stage");
        service.AdvanceObjective(player.Id, "test:two-stage", "obj-0", 1);

        var active = repo.GetOrCreate(player.Id).Active[0];
        active.StageIndex.Should().Be(1);
        active.Objectives.Should().ContainSingle(o => o.ObjectiveId == "obj-1");
    }

    [Fact]
    public void AbandonQuest_RemovesFromActive()
    {
        var (service, repo, _, player) = Setup(MakeKillQuest());
        service.AcceptQuest(player, "test:kill-quest");
        service.AbandonQuest(player.Id, "test:kill-quest");
        repo.GetOrCreate(player.Id).Active.Should().BeEmpty();
    }

    [Fact]
    public void AbandonQuest_Ignored_WhenNotAbandonable()
    {
        var quest = MakeKillQuest();
        quest.Abandonable = false;
        var (service, repo, _, player) = Setup(quest);
        service.AcceptQuest(player, "test:kill-quest");
        service.AbandonQuest(player.Id, "test:kill-quest");
        repo.GetOrCreate(player.Id).Active.Should().HaveCount(1);
    }

    // --- New tests for Task 5 ---

    [Fact]
    public void AcceptQuest_IncludesBannerText_InQuestStartedEvent_WhenNotSecret()
    {
        var quest = MakeKillQuest();
        var (service, _, bus, player, _, _, _) = SetupFull(quest);

        GameEvent? startedEvent = null;
        bus.Subscribe("quest.started", evt => { startedEvent = evt; });

        service.AcceptQuest(player, quest.Id);

        startedEvent.Should().NotBeNull();
        startedEvent!.Data.ContainsKey("bannerText").Should().BeTrue();
        startedEvent.Data["bannerText"]?.ToString().Should().Contain(quest.Name);
    }

    [Fact]
    public void AcceptQuest_NoBannerText_InEvent_WhenSilent()
    {
        var quest = MakeKillQuest();
        var (service, _, bus, player, _, _, _) = SetupFull(quest);

        GameEvent? startedEvent = null;
        bus.Subscribe("quest.started", evt => { startedEvent = evt; });

        service.AcceptQuest(player, quest.Id, silent: true);

        startedEvent.Should().NotBeNull();
        startedEvent!.Data.ContainsKey("bannerText").Should().BeFalse();
    }

    [Fact]
    public void AcceptQuest_NoBannerText_InEvent_WhenQuestIsSecret()
    {
        var quest = MakeKillQuest();
        quest.Secret = true;
        var (service, _, bus, player, _, _, _) = SetupFull(quest);

        GameEvent? startedEvent = null;
        bus.Subscribe("quest.started", evt => { startedEvent = evt; });

        service.AcceptQuest(player, quest.Id);

        startedEvent!.Data.ContainsKey("bannerText").Should().BeFalse();
    }

    [Fact]
    public void AcceptQuest_NoBannerText_InEvent_WhenScriptOnGrantedReturnsTrue()
    {
        var quest = MakeKillQuest();
        quest.Script = "scripts/quests/test.js";
        var (service, _, bus, player, scriptLoader, _, _) = SetupFull(quest);
        scriptLoader.OnGrantedReturn = true;

        GameEvent? startedEvent = null;
        bus.Subscribe("quest.started", evt => { startedEvent = evt; });

        service.AcceptQuest(player, quest.Id);

        startedEvent!.Data.ContainsKey("bannerText").Should().BeFalse();
    }

    [Fact]
    public void AcceptQuest_SavesQuestState()
    {
        var quest = MakeKillQuest();
        var (service, _, _, player, _, _, persistence) = SetupFull(quest);

        service.AcceptQuest(player, quest.Id);

        persistence.SaveCallCount.Should().Be(1);
        persistence.LastSavedName.Should().Be("Alice");
    }

    [Fact]
    public void CompleteQuest_DispatchesRewards()
    {
        var quest = MakeKillQuest();
        var (service, _, _, player, _, rewards, _) = SetupFull(quest);
        service.AcceptQuest(player, quest.Id);

        service.AdvanceObjective(player.Id, quest.Id, "kill-trollocs", 3);

        rewards.DispatchCallCount.Should().Be(1);
        rewards.LastRewards.Should().BeSameAs(quest.Rewards);
        rewards.LastPlayer.Should().BeSameAs(player);
    }

    [Fact]
    public void CompleteQuest_SavesQuestState()
    {
        var quest = MakeKillQuest();
        var (service, _, _, player, _, _, persistence) = SetupFull(quest);
        service.AcceptQuest(player, quest.Id);
        persistence.SaveCallCount = 0; // reset after accept

        service.AdvanceObjective(player.Id, quest.Id, "kill-trollocs", 3);

        persistence.SaveCallCount.Should().BeGreaterThan(0);
    }
}

internal class FakeQuestScriptLoader : IQuestScriptLoader
{
    public bool OnGrantedReturn { get; set; } = false;
    public bool HasScript(string questId) => OnGrantedReturn;
    public bool CallOnGranted(string questId, Guid playerId) => OnGrantedReturn;
    public void CallOnObjectiveAdvanced(string questId, Guid playerId, string objectiveId, int current, int required) { }
    public void CallOnCompleted(string questId, Guid playerId) { }
    public void CallOnStageAdvanced(string questId, Guid playerId, int stageIndex) { }
}

internal class FakeQuestRewardDispatcher : IQuestRewardDispatcher
{
    public int DispatchCallCount { get; set; }
    public QuestReward? LastRewards { get; private set; }
    public Entity? LastPlayer { get; private set; }

    public void Dispatch(Entity player, QuestReward? rewards)
    {
        DispatchCallCount++;
        LastPlayer = player;
        LastRewards = rewards;
    }
}

internal class FakePersistence : IQuestPersistence
{
    public int SaveCallCount { get; set; }
    public string? LastSavedName { get; private set; }

    public void Save(string playerName, QuestState state)
    {
        SaveCallCount++;
        LastSavedName = playerName;
    }
}
