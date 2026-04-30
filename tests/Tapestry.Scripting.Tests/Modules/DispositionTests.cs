using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Mobs;
using Tapestry.Scripting;
using Tapestry.Shared;

namespace Tapestry.Scripting.Tests.Modules;

public class DispositionTests
{
    private IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void HostileTag_BypassesDisposition_FiresAggro()
    {
        var sp = BuildProvider();
        var world = sp.GetRequiredService<World>();
        var evaluator = sp.GetRequiredService<DispositionEvaluator>();
        var eventBus = sp.GetRequiredService<EventBus>();
        var published = new List<GameEvent>();
        eventBus.Subscribe("mob.aggro", e => { published.Add(e); });

        var mob = new Entity("npc", "Elf");
        mob.AddTag("npc");
        mob.AddTag("hostile");
        mob.SetProperty("disposition", new DispositionDefinition { Default = "neutral" });
        world.TrackEntity(mob);

        var player = new Entity("player", "Tester");
        player.AddTag("player");
        world.TrackEntity(player);

        evaluator.EvaluateForMob(mob, player);
        // hostile tag fires mob.aggro directly — Engage is idempotent for already-in-combat mobs
        Assert.Single(published);
    }

    [Fact]
    public void NeutralAlignment_MobWithRules_FallsToDefault()
    {
        var sp = BuildProvider();
        var world = sp.GetRequiredService<World>();
        var evaluator = sp.GetRequiredService<DispositionEvaluator>();
        var alignmentMgr = sp.GetRequiredService<AlignmentManager>();
        var eventBus = sp.GetRequiredService<EventBus>();
        var published = new List<GameEvent>();
        eventBus.Subscribe("mob.disposition.wary", e => { published.Add(e); });
        eventBus.Subscribe("mob.disposition.friendly", e => { published.Add(e); });
        eventBus.Subscribe("mob.aggro", e => { published.Add(e); });

        var mob = new Entity("npc", "Zealot");
        mob.AddTag("npc");
        mob.SetProperty("disposition", new DispositionDefinition
        {
            Default = "neutral",
            Rules = new List<DispositionRule>
            {
                new() { When = new DispositionCondition { MinAlignment = 300 }, Reaction = "friendly" },
                new() { When = new DispositionCondition { MaxAlignment = -100 }, Reaction = "hostile" }
            }
        });
        world.TrackEntity(mob);

        var player = new Entity("player", "Tester");
        player.AddTag("player");
        world.TrackEntity(player);
        alignmentMgr.Set(player.Id, 0, "init");  // neutral, matches default

        evaluator.EvaluateForMob(mob, player);
        Assert.Empty(published);  // neutral default: no event dispatched
    }

    [Fact]
    public void FriendlyAlignment_DispatchesFriendlyEvent()
    {
        var sp = BuildProvider();
        var world = sp.GetRequiredService<World>();
        var evaluator = sp.GetRequiredService<DispositionEvaluator>();
        var alignmentMgr = sp.GetRequiredService<AlignmentManager>();
        var eventBus = sp.GetRequiredService<EventBus>();
        var published = new List<GameEvent>();
        eventBus.Subscribe("mob.disposition.friendly", e => { published.Add(e); });

        var mob = new Entity("npc", "Zealot");
        mob.AddTag("npc");
        mob.SetProperty("disposition", new DispositionDefinition
        {
            Default = "neutral",
            Rules = new List<DispositionRule>
            {
                new() { When = new DispositionCondition { MinAlignment = 300 }, Reaction = "friendly" }
            }
        });
        world.TrackEntity(mob);

        var player = new Entity("player", "Tester");
        player.AddTag("player");
        world.TrackEntity(player);
        alignmentMgr.Set(player.Id, 400, "init");  // good — triggers friendly

        evaluator.EvaluateForMob(mob, player);
        Assert.Single(published);
    }

    [Fact]
    public void MobWithoutDispositionBlock_NoEvents()
    {
        var sp = BuildProvider();
        var world = sp.GetRequiredService<World>();
        var evaluator = sp.GetRequiredService<DispositionEvaluator>();
        var eventBus = sp.GetRequiredService<EventBus>();
        var published = new List<GameEvent>();
        eventBus.Subscribe("mob.aggro", e => { published.Add(e); });
        eventBus.Subscribe("mob.disposition.wary", e => { published.Add(e); });
        eventBus.Subscribe("mob.disposition.friendly", e => { published.Add(e); });

        var mob = new Entity("npc", "Plain Mob");
        mob.AddTag("npc");
        world.TrackEntity(mob);

        var player = new Entity("player", "Tester");
        player.AddTag("player");
        world.TrackEntity(player);

        evaluator.EvaluateForMob(mob, player);
        Assert.Empty(published);
    }
}
