using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Combat;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Alignment;

public class CombatAlignmentShiftTests
{
    [Fact]
    public void HandleEntityDeath_PublishesShiftCheckWithKillerAndVictim()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        var sp = services.BuildServiceProvider();
        var world = sp.GetRequiredService<World>();
        var eventBus = sp.GetRequiredService<EventBus>();
        var combatMgr = sp.GetRequiredService<CombatManager>();

        var killer = new Entity("player", "Killer");
        var victim = new Entity("npc", "Victim");
        world.TrackEntity(killer);
        world.TrackEntity(victim);

        var checkEvents = new List<GameEvent>();
        eventBus.Subscribe("alignment.shift.check", e => checkEvents.Add(e));

        combatMgr.HandleEntityDeath(victim.Id, killer.Id);

        Assert.Single(checkEvents);
        Assert.Equal(killer.Id.ToString(), checkEvents[0].Data["actorId"]?.ToString());
        Assert.Equal(victim.Id.ToString(), checkEvents[0].Data["targetId"]?.ToString());
        Assert.Equal("combat.kill", checkEvents[0].Data["reason"]?.ToString());
    }

    [Fact]
    public void HandleEntityDeath_SubscriberSetsPositiveDelta_ShiftsKillerAlignment()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        var sp = services.BuildServiceProvider();
        var world = sp.GetRequiredService<World>();
        var eventBus = sp.GetRequiredService<EventBus>();
        var combatMgr = sp.GetRequiredService<CombatManager>();
        var alignmentMgr = sp.GetRequiredService<AlignmentManager>();

        var killer = new Entity("player", "Killer");
        var victim = new Entity("npc", "Victim");
        world.TrackEntity(killer);
        world.TrackEntity(victim);

        eventBus.Subscribe("alignment.shift.check", e => e.Data["suggestedDelta"] = 3);

        combatMgr.HandleEntityDeath(victim.Id, killer.Id);

        Assert.Equal(3, alignmentMgr.Get(killer.Id));
    }

    [Fact]
    public void HandleEntityDeath_SubscriberCancels_NoAlignmentChange()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        var sp = services.BuildServiceProvider();
        var world = sp.GetRequiredService<World>();
        var eventBus = sp.GetRequiredService<EventBus>();
        var combatMgr = sp.GetRequiredService<CombatManager>();
        var alignmentMgr = sp.GetRequiredService<AlignmentManager>();

        var killer = new Entity("player", "Killer");
        var victim = new Entity("npc", "Victim");
        world.TrackEntity(killer);
        world.TrackEntity(victim);

        eventBus.Subscribe("alignment.shift.check", e =>
        {
            e.Data["suggestedDelta"] = 99;
            e.Data["cancel"] = true;
        });

        combatMgr.HandleEntityDeath(victim.Id, killer.Id);
        Assert.Equal(0, alignmentMgr.Get(killer.Id));
    }
}
