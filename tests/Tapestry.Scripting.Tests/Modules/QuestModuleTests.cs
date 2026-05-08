using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Quests;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class QuestModuleTests
{
    private (JintRuntime rt, World world, QuestRegistry registry) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<World>(), provider.GetRequiredService<QuestRegistry>());
    }

    private static QuestDefinition MakeQuest(string id) =>
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

        var result = rt.Evaluate($"tapestry.quests.isActive('{playerId}', 'some-quest')");

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

        rt.Evaluate($"tapestry.quests.offer('{playerId}', 'test-quest-active')");

        var result = rt.Evaluate($"tapestry.quests.isActive('{playerId}', 'test-quest-active')");

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

        rt.Evaluate($"tapestry.quests.offer('{playerId}', 'test-quest-offer')");

        var result = rt.Evaluate($"tapestry.quests.isActive('{playerId}', 'test-quest-offer')");
        result.Should().Be(true);
    }
}
