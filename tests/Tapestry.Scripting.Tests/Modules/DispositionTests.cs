using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Rest;
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
    public void HostileDisposition_BypassesRules_FiresAggro()
    {
        var sp = BuildProvider();
        var world = sp.GetRequiredService<World>();
        var evaluator = sp.GetRequiredService<DispositionEvaluator>();
        var eventBus = sp.GetRequiredService<EventBus>();
        var published = new List<GameEvent>();
        eventBus.Subscribe("mob.aggro", e => { published.Add(e); });

        var mob = new Entity("npc", "Elf");
        mob.AddTag("npc");
        mob.Disposition = Disposition.Hostile;
        mob.DispositionRules = new DispositionDefinition { Default = "neutral" };
        world.TrackEntity(mob);

        var player = new Entity("player", "Tester");
        player.AddTag("player");
        world.TrackEntity(player);

        evaluator.EvaluateForMob(mob, player);
        // Disposition.Hostile bypasses rules and fires mob.aggro — Engage is idempotent for already-in-combat mobs
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
        mob.DispositionRules = new DispositionDefinition
        {
            Default = "neutral",
            Rules = new List<DispositionRule>
            {
                new() { When = new DispositionCondition { MinAlignment = 300 }, Reaction = "friendly" },
                new() { When = new DispositionCondition { MaxAlignment = -100 }, Reaction = "hostile" }
            }
        };
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
        mob.DispositionRules = new DispositionDefinition
        {
            Default = "neutral",
            Rules = new List<DispositionRule>
            {
                new() { When = new DispositionCondition { MinAlignment = 300 }, Reaction = "friendly" }
            }
        };
        world.TrackEntity(mob);

        var player = new Entity("player", "Tester");
        player.AddTag("player");
        world.TrackEntity(player);
        alignmentMgr.Set(player.Id, 400, "init");  // good — triggers friendly

        evaluator.EvaluateForMob(mob, player);
        Assert.Single(published);
    }

    [Fact]
    public void HostileBaseDisposition_NoRulesBlock_FiresAggro()
    {
        // base_disposition: hostile produces Disposition.Hostile with NO DispositionRules
        // (only a `disposition:` block creates rules). Such a mob must still aggro.
        var sp = BuildProvider();
        var world = sp.GetRequiredService<World>();
        var evaluator = sp.GetRequiredService<DispositionEvaluator>();
        var eventBus = sp.GetRequiredService<EventBus>();
        var published = new List<GameEvent>();
        eventBus.Subscribe("mob.aggro", e => { published.Add(e); });

        var mob = new Entity("npc", "a goblin");
        mob.AddTag("npc");
        mob.Disposition = Disposition.Hostile;
        // DispositionRules deliberately left null.
        world.TrackEntity(mob);

        var player = new Entity("player", "Tester");
        player.AddTag("player");
        world.TrackEntity(player);

        evaluator.EvaluateForMob(mob, player);
        Assert.Single(published);
    }

    [Fact]
    public void EvaluateRoom_HostileBaseDisposition_Aggros()
    {
        // The room-entry aggro path must include base_disposition-only hostile mobs.
        var sp = BuildProvider();
        var world = sp.GetRequiredService<World>();
        var evaluator = sp.GetRequiredService<DispositionEvaluator>();
        var eventBus = sp.GetRequiredService<EventBus>();
        var published = new List<GameEvent>();
        eventBus.Subscribe("mob.aggro", e => { published.Add(e); });

        var room = new Room("zone:room1", "Room", "Test.");
        world.AddRoom(room);

        var mob = new Entity("npc", "a goblin") { LocationRoomId = "zone:room1" };
        mob.AddTag("npc");
        mob.Disposition = Disposition.Hostile;
        room.AddEntity(mob);
        world.TrackEntity(mob);

        var player = new Entity("player", "Tester") { LocationRoomId = "zone:room1" };
        player.AddTag("player");
        room.AddEntity(player);
        world.TrackEntity(player);

        evaluator.EvaluateRoom("zone:room1", player.Id, aggroOnly: true);
        Assert.Single(published);
    }

    [Fact]
    public void SleepingHostileMob_DoesNotAggro()
    {
        // A sleeping aggro mob is unaware -- only being attacked (combat auto-wake)
        // rouses it. Uses an explicit rules block so it WOULD aggro if awake.
        var sp = BuildProvider();
        var world = sp.GetRequiredService<World>();
        var evaluator = sp.GetRequiredService<DispositionEvaluator>();
        var eventBus = sp.GetRequiredService<EventBus>();
        var published = new List<GameEvent>();
        eventBus.Subscribe("mob.aggro", e => { published.Add(e); });

        var mob = new Entity("npc", "a sleepy guard");
        mob.AddTag("npc");
        mob.Disposition = Disposition.Hostile;
        mob.DispositionRules = new DispositionDefinition { Default = "neutral" };
        mob.SetProperty(RestProperties.RestState, RestProperties.StateSleeping);
        world.TrackEntity(mob);

        var player = new Entity("player", "Tester");
        player.AddTag("player");
        world.TrackEntity(player);

        evaluator.EvaluateForMob(mob, player);
        Assert.Empty(published);
    }

    [Fact]
    public void RestingHostileMob_StillAggros()
    {
        // Resting mobs stay aware by design -- they can still notice and aggro.
        var sp = BuildProvider();
        var world = sp.GetRequiredService<World>();
        var evaluator = sp.GetRequiredService<DispositionEvaluator>();
        var eventBus = sp.GetRequiredService<EventBus>();
        var published = new List<GameEvent>();
        eventBus.Subscribe("mob.aggro", e => { published.Add(e); });

        var mob = new Entity("npc", "a resting guard");
        mob.AddTag("npc");
        mob.Disposition = Disposition.Hostile;
        mob.SetProperty(RestProperties.RestState, RestProperties.StateResting);
        world.TrackEntity(mob);

        var player = new Entity("player", "Tester");
        player.AddTag("player");
        world.TrackEntity(player);

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
