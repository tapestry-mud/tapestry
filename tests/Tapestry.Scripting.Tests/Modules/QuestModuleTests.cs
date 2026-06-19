using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Quests;
using Tapestry.Scripting;
using Tapestry.Shared;
using Tapestry.Scripting.Tests;

namespace Tapestry.Scripting.Tests.Modules;

public class QuestModuleTests
{
    private (JintRuntime rt, World world, QuestRegistry registry, ServiceProvider provider) BuildRuntimeFull()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<World>(), provider.GetRequiredService<QuestRegistry>(), provider);
    }

    private (JintRuntime rt, World world, QuestRegistry registry) BuildRuntime()
    {
        var (rt, world, registry, _) = BuildRuntimeFull();
        return (rt, world, registry);
    }

    private static QuestDefinition MakeQuest(string id, string? hint = null) =>
        new()
        {
            Id = id,
            Name = "Test Quest",
            Type = "side",
            Abandonable = true,
            Stages =
            [
                new QuestStage
                {
                    Id = "stage-0",
                    Hint = hint,
                    Objectives =
                    [
                        new QuestObjective
                        {
                            Id = "obj-0",
                            Type = "kill",
                            Target = "goblin",
                            Count = 1,
                            Description = "Kill a goblin",
                        }
                    ]
                }
            ]
        };

    [Fact]
    public void IsActive_ReturnsFalse_WhenQuestNotStarted()
    {
        var (rt, world, _) = BuildRuntime();

        var player = new Entity("player", "Rand");
        world.TrackEntity(player);
        var playerId = player.Id.ToString();

        var result = EsmTest.Eval(rt, $"tapestry.quests.isActive('{playerId}', 'some-quest')");

        result.Should().Be(false);
    }

    [Fact]
    public void IsActive_ReturnsTrue_WhenQuestIsActive()
    {
        var (rt, world, registry) = BuildRuntime();

        var questDef = MakeQuest("test-quest-active");
        registry.RegisterForTest(questDef);

        var player = new Entity("player", "Rand");
        world.TrackEntity(player);
        var playerId = player.Id.ToString();

        EsmTest.Eval(rt, $"tapestry.quests.offer('{playerId}', 'test-quest-active')");

        var result = EsmTest.Eval(rt, $"tapestry.quests.isActive('{playerId}', 'test-quest-active')");

        result.Should().Be(true);
    }

    [Fact]
    public void Offer_AcceptsQuest_WhenPrereqsMet()
    {
        var (rt, world, registry) = BuildRuntime();

        var questDef = MakeQuest("test-quest-offer");
        registry.RegisterForTest(questDef);

        var player = new Entity("player", "Mat");
        world.TrackEntity(player);
        var playerId = player.Id.ToString();

        EsmTest.Eval(rt, $"tapestry.quests.offer('{playerId}', 'test-quest-offer')");

        var result = EsmTest.Eval(rt, $"tapestry.quests.isActive('{playerId}', 'test-quest-offer')");
        result.Should().Be(true);
    }

    [Fact]
    public void GetHint_ReturnsCurrentStageHint_WhenQuestIsActive()
    {
        var (rt, world, registry) = BuildRuntime();

        var questDef = MakeQuest("test-quest-gethint", hint: "check the old ruins");
        registry.RegisterForTest(questDef);

        var player = new Entity("player", "Perrin");
        world.TrackEntity(player);
        var playerId = player.Id.ToString();

        EsmTest.Eval(rt, $"tapestry.quests.offer('{playerId}', 'test-quest-gethint')");

        var result = EsmTest.Eval(rt, $"tapestry.quests.getHint('{playerId}', 'test-quest-gethint')");

        result.Should().Be("check the old ruins");
    }

    [Fact]
    public void GetHint_ReturnsNull_WhenQuestIsNotActive()
    {
        var (rt, world, _) = BuildRuntime();

        var player = new Entity("player", "Egwene");
        world.TrackEntity(player);
        var playerId = player.Id.ToString();

        var result = EsmTest.Eval(rt, $"tapestry.quests.getHint('{playerId}', 'nonexistent-quest')");

        result.Should().BeNull();
    }

    [Fact]
    public void GetActiveHints_ReturnsAllActiveQuestHints()
    {
        var (rt, world, registry) = BuildRuntime();

        var q1 = MakeQuest("hint-quest-1", hint: "first hint");
        var q2 = MakeQuest("hint-quest-2", hint: "second hint");
        registry.RegisterForTest(q1);
        registry.RegisterForTest(q2);

        var player = new Entity("player", "Moiraine");
        world.TrackEntity(player);
        var playerId = player.Id.ToString();

        EsmTest.Eval(rt, $"tapestry.quests.offer('{playerId}', 'hint-quest-1')");
        EsmTest.Eval(rt, $"tapestry.quests.offer('{playerId}', 'hint-quest-2')");

        var result = EsmTest.Eval(rt, $"tapestry.quests.getActiveHints('{playerId}').length");

        result.Should().Be(2);
    }

    [Fact]
    public void Offer_WithSilentOption_DoesNotIncludeBannerTextInEvent()
    {
        var (rt, world, registry, provider) = BuildRuntimeFull();
        var eventBus = provider.GetRequiredService<EventBus>();

        var questDef = MakeQuest("silent-quest");
        registry.RegisterForTest(questDef);

        var player = new Entity("player", "Lan");
        world.TrackEntity(player);
        var playerId = player.Id.ToString();

        GameEvent? capturedEvent = null;
        eventBus.Subscribe("quest.started", evt => { capturedEvent = evt; });

        EsmTest.Eval(rt, $"tapestry.quests.offer('{playerId}', 'silent-quest', {{ silent: true }})");

        capturedEvent.Should().NotBeNull();
        capturedEvent!.Data.ContainsKey("bannerText").Should().BeFalse();
    }

    [Fact]
    public void HasQuestMarker_ReturnsFalse_WhenNoMarkerSet()
    {
        var (rt, world, _) = BuildRuntime();

        var player = new Entity("player", "Nynaeve");
        world.TrackEntity(player);
        var playerId = player.Id.ToString();

        var result = EsmTest.Eval(rt, $"tapestry.quests.hasQuestMarker('{playerId}', 'some-template')");

        result.Should().Be(false);
    }
}
