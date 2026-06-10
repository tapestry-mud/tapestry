// tests/Tapestry.Engine.Tests/Mobs/MobAIManagerTests.cs
using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Mobs;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Mobs;

public class MobAIManagerTests
{
    /// <summary>Deterministic clock: 1 timestamp tick = 1ms. Behaviors advance it
    /// to simulate cost; the manager's budget math sees exactly what we script.</summary>
    private sealed class FakeTime : TimeProvider
    {
        private long _timestamp;
        public override long GetTimestamp() => _timestamp;
        public override long TimestampFrequency => 1000;
        public void AdvanceMs(long ms) => _timestamp += ms;
    }

    private static MobAIManager BuildManager(World world)
    {
        var eventBus = new EventBus();
        var combatManager = new CombatManager(world, eventBus);
        var alignmentConfig = new AlignmentConfig();
        var alignmentManager = new AlignmentManager(world, eventBus, alignmentConfig);
        var dispositionEvaluator = new DispositionEvaluator(world, eventBus, alignmentManager);
        var metrics = new TapestryMetrics();
        return new MobAIManager(world, eventBus, combatManager, dispositionEvaluator,
            NullLogger<MobAIManager>.Instance, metrics);
    }

    private static MobAIManager BuildManager(World world, Tapestry.Data.ServerConfig config,
        TimeProvider time)
    {
        var eventBus = new EventBus();
        var combatManager = new CombatManager(world, eventBus);
        var alignmentConfig = new AlignmentConfig();
        var alignmentManager = new AlignmentManager(world, eventBus, alignmentConfig);
        var dispositionEvaluator = new DispositionEvaluator(world, eventBus, alignmentManager);
        var metrics = new TapestryMetrics();
        return new MobAIManager(world, eventBus, combatManager, dispositionEvaluator,
            NullLogger<MobAIManager>.Instance, metrics, gate: null, config: config, time: time);
    }

    private static Entity AddMob(World world, Room room, string behavior, string name)
    {
        var mob = new Entity("npc", name);
        mob.SetProperty("behavior", behavior);
        mob.SetProperty("template_id", "core:goblin");
        mob.AddTag("npc");
        mob.LocationRoomId = room.Id;
        room.AddEntity(mob);
        world.TrackEntity(mob);
        return mob;
    }

    [Fact]
    public void ActivateArea_MarksAreaActive()
    {
        var world = new World();
        var manager = BuildManager(world);

        manager.ActivateArea("starter-town");

        Assert.True(manager.IsAreaActive("starter-town"));
    }

    [Fact]
    public void DeactivateArea_MarksAreaDormant()
    {
        var world = new World();
        var manager = BuildManager(world);

        manager.ActivateArea("starter-town");
        manager.DeactivateArea("starter-town");

        Assert.False(manager.IsAreaActive("starter-town"));
    }

    [Fact]
    public void PlayerEnteredRoom_ActivatesArea()
    {
        var world = new World();
        var manager = BuildManager(world);

        manager.PlayerEnteredRoom("core:town-square");

        Assert.True(manager.IsAreaActive("core"));
    }

    [Fact]
    public void PlayerLeftRoom_LastPlayer_DeactivatesArea()
    {
        var world = new World();
        var room = new Room("core:town-square", "Town Square", "A square.");
        world.AddRoom(room);
        var manager = BuildManager(world);

        manager.PlayerEnteredRoom("core:town-square");
        Assert.True(manager.IsAreaActive("core"));

        manager.PlayerLeftRoom("core:town-square");
        Assert.False(manager.IsAreaActive("core"));
    }

    [Fact]
    public void RegisterBehavior_StoresHandler()
    {
        var world = new World();
        var manager = BuildManager(world);

        manager.RegisterBehavior("test-behavior", (_) => { });

        Assert.True(manager.HasBehavior("test-behavior"));
    }

    [Fact]
    public void Tick_CallsBehaviorHandler_ForActiveAreaMobs()
    {
        var world = new World();
        var room = new Room("core:town-square", "Town Square", "A square.");
        world.AddRoom(room);

        var mobEntity = new Entity("npc", "a goblin");
        mobEntity.SetProperty("behavior", "test-behavior");
        mobEntity.SetProperty("template_id", "core:goblin");
        mobEntity.AddTag("npc");
        mobEntity.LocationRoomId = "core:town-square";
        room.AddEntity(mobEntity);
        world.TrackEntity(mobEntity);

        var manager = BuildManager(world);
        var handlerCalled = false;
        manager.RegisterBehavior("test-behavior", (mob) => { handlerCalled = true; });

        world.SwapTagBuffers();
        manager.PlayerEnteredRoom("core:town-square");
        manager.Tick();

        Assert.True(handlerCalled);
    }

    [Fact]
    public void Tick_SkipsBehavior_ForDormantAreaMobs()
    {
        var world = new World();
        var room = new Room("core:town-square", "Town Square", "A square.");
        world.AddRoom(room);

        var mobEntity = new Entity("npc", "a goblin");
        mobEntity.SetProperty("behavior", "test-behavior");
        mobEntity.SetProperty("template_id", "core:goblin");
        mobEntity.AddTag("npc");
        mobEntity.LocationRoomId = "core:town-square";
        room.AddEntity(mobEntity);
        world.TrackEntity(mobEntity);

        var manager = BuildManager(world);
        var handlerCalled = false;
        manager.RegisterBehavior("test-behavior", (mob) => { handlerCalled = true; });

        // No player in area -- dormant
        manager.Tick();

        Assert.False(handlerCalled);
    }

    [Fact]
    public void GetTicksSinceLastAction_TracksPerMob()
    {
        var world = new World();
        var manager = BuildManager(world);
        var entityId = Guid.NewGuid();

        manager.RecordAction(entityId);
        manager.IncrementTick();
        manager.IncrementTick();
        manager.IncrementTick();

        Assert.Equal(3, manager.GetTicksSinceLastAction(entityId));
    }

    [Fact]
    public void Tick_PublishesMobAiTickEvent_ForEachActiveMob()
    {
        var world = new World();
        var eventBus = new EventBus();
        var combatManager = new CombatManager(world, eventBus);
        var alignmentConfig = new AlignmentConfig();
        var alignmentManager = new AlignmentManager(world, eventBus, alignmentConfig);
        var dispositionEvaluator = new DispositionEvaluator(world, eventBus, alignmentManager);
        var manager = new MobAIManager(world, eventBus, combatManager, dispositionEvaluator,
            NullLogger<MobAIManager>.Instance, new TapestryMetrics());

        var room = new Room("core:town-square", "Town Square", "A square.");
        world.AddRoom(room);

        var mob = new Entity("npc", "Guard");
        mob.LocationRoomId = "core:town-square";
        mob.SetProperty("behavior", "stationary");
        mob.AddTag("npc");
        room.AddEntity(mob);
        world.TrackEntity(mob);

        manager.RegisterBehavior("stationary", _ => { });
        world.SwapTagBuffers();
        manager.PlayerEnteredRoom("core:town-square");

        var received = new List<Tapestry.Shared.GameEvent>();
        eventBus.Subscribe("mob.ai.tick", evt => { received.Add(evt); });

        manager.Tick();

        Assert.Single(received);
        Assert.Equal(mob.Id.ToString(), received[0].Data["entityId"]);
        Assert.Equal("core:town-square", received[0].Data["roomId"]);
    }

    [Fact]
    public void Tick_BehaviorAndDisposition_RunInSinglePass()
    {
        var world = new World();
        var room = new Room("zone:room1", "Room", "Test.");
        world.AddRoom(room);
        var eventBus = new EventBus();
        var combatManager = new CombatManager(world, eventBus);
        var alignmentConfig = new AlignmentConfig();
        var alignmentManager = new AlignmentManager(world, eventBus, alignmentConfig);
        var dispositionEvaluator = new DispositionEvaluator(world, eventBus, alignmentManager);
        var ai = new MobAIManager(world, eventBus, combatManager, dispositionEvaluator,
            NullLogger<MobAIManager>.Instance, new TapestryMetrics());

        var dispatched = new List<string>();
        ai.RegisterBehavior("patrol", ctx => { dispatched.Add(ctx.Name); });
        ai.ActivateArea("zone");

        var npc = new Entity("npc", "Goblin");
        npc.AddTag("npc");
        npc.SetProperty("behavior", "patrol");
        npc.LocationRoomId = "zone:room1";
        room.AddEntity(npc);
        world.TrackEntity(npc);
        world.SwapTagBuffers();

        ai.Tick();

        dispatched.Should().ContainSingle("Goblin");
    }

    [Fact]
    public void Tick_NonNpcType_IsNotDispatched()
    {
        var world = new World();
        var room = new Room("zone:room1", "Room", "Test.");
        world.AddRoom(room);
        var eventBus = new EventBus();
        var combatManager = new CombatManager(world, eventBus);
        var alignmentConfig = new AlignmentConfig();
        var alignmentManager = new AlignmentManager(world, eventBus, alignmentConfig);
        var dispositionEvaluator = new DispositionEvaluator(world, eventBus, alignmentManager);
        var ai = new MobAIManager(world, eventBus, combatManager, dispositionEvaluator,
            NullLogger<MobAIManager>.Instance, new TapestryMetrics());

        var dispatched = new List<string>();
        ai.RegisterBehavior("patrol", ctx => { dispatched.Add(ctx.Name); });
        ai.ActivateArea("zone");

        // Entity with non-npc type should not be dispatched by MobAIManager
        var item = new Entity("item", "Sword");
        item.SetProperty("behavior", "patrol");
        item.LocationRoomId = "zone:room1";
        room.AddEntity(item);
        world.TrackEntity(item);
        world.SwapTagBuffers();

        ai.Tick();

        dispatched.Should().BeEmpty();
    }

    [Fact]
    public void Tick_MobAiTickEvent_PublishedOnce_PerNpc_PerTick()
    {
        var world = new World();
        var room = new Room("zone:room1", "Room", "Test.");
        world.AddRoom(room);
        var eventBus = new EventBus();
        var combatManager = new CombatManager(world, eventBus);
        var alignmentConfig = new AlignmentConfig();
        var alignmentManager = new AlignmentManager(world, eventBus, alignmentConfig);
        var dispositionEvaluator = new DispositionEvaluator(world, eventBus, alignmentManager);
        var ai = new MobAIManager(world, eventBus, combatManager, dispositionEvaluator,
            NullLogger<MobAIManager>.Instance, new TapestryMetrics());

        var tickEvents = new List<GameEvent>();
        eventBus.Subscribe("mob.ai.tick", evt => { tickEvents.Add(evt); });
        ai.ActivateArea("zone");
        ai.RegisterBehavior("wander", _ => { });

        var npc = new Entity("npc", "Goblin");
        npc.AddTag("npc");
        npc.SetProperty("behavior", "wander");
        npc.LocationRoomId = "zone:room1";
        room.AddEntity(npc);
        world.TrackEntity(npc);
        world.SwapTagBuffers();

        ai.Tick();

        tickEvents.Should().ContainSingle(e => (string)e.Data["name"]! == "Goblin");
    }

    [Fact]
    public void Tick_NpcInInactiveArea_IsSkipped()
    {
        var world = new World();
        var room = new Room("zone:room1", "Room", "Test.");
        world.AddRoom(room);
        var eventBus = new EventBus();
        var combatManager = new CombatManager(world, eventBus);
        var alignmentConfig = new AlignmentConfig();
        var alignmentManager = new AlignmentManager(world, eventBus, alignmentConfig);
        var dispositionEvaluator = new DispositionEvaluator(world, eventBus, alignmentManager);
        var ai = new MobAIManager(world, eventBus, combatManager, dispositionEvaluator,
            NullLogger<MobAIManager>.Instance, new TapestryMetrics());

        var dispatched = new List<string>();
        ai.RegisterBehavior("patrol", ctx => { dispatched.Add(ctx.Name); });
        // Area NOT activated

        var npc = new Entity("npc", "Goblin");
        npc.AddTag("npc");
        npc.SetProperty("behavior", "patrol");
        npc.LocationRoomId = "zone:room1";
        room.AddEntity(npc);
        world.TrackEntity(npc);
        world.SwapTagBuffers();

        ai.Tick();

        dispatched.Should().BeEmpty();
    }

    [Fact]
    public void Tick_RecordsMobAiPhaseMetrics()
    {
        var world = new World();
        var eventBus = new EventBus();
        var metrics = new TapestryMetrics();
        var alignmentConfig = new AlignmentConfig();
        var alignmentManager = new AlignmentManager(world, eventBus, alignmentConfig);
        var combat = new CombatManager(world, eventBus);
        var disposition = new DispositionEvaluator(world, eventBus, alignmentManager);
        var mobAI = new MobAIManager(world, eventBus, combat, disposition,
            NullLogger<MobAIManager>.Instance, metrics);

        // A behavior that sleeps, so the "behavior" phase is clearly non-zero.
        mobAI.RegisterBehavior("slow", _ => Thread.Sleep(20));

        var room = new Room("zone:room", "Room", "A room.");
        world.AddRoom(room);
        mobAI.ActivateArea("zone");

        var mob = new Entity("npc", "Slowpoke") { LocationRoomId = "zone:room" };
        mob.SetProperty(MobProperties.Behavior, "slow");
        room.AddEntity(mob);
        world.TrackEntity(mob);

        var phases = new List<(string phase, double ms)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Name == "tapestry.mob_ai.phase_ms") { l.EnableMeasurementEvents(inst); }
        };
        listener.SetMeasurementEventCallback<double>((inst, value, tags, state) =>
        {
            string? phase = null;
            foreach (var t in tags)
            {
                if (t.Key == "phase") { phase = t.Value?.ToString(); }
            }
            if (phase != null) { phases.Add((phase, value)); }
        });
        listener.Start();

        mobAI.Tick();

        phases.Select(p => p.phase).Should().Contain(
            new[] { "scan", "behavior", "publish", "disposition" });
        phases.First(p => p.phase == "behavior").ms.Should().BeGreaterThan(10);
    }

    [Fact]
    public void MobAiSection_Defaults_AreGroundedValues()
    {
        var config = new Tapestry.Data.ServerConfig();

        Assert.Equal(25, config.MobAi.TickBudgetMs);
        Assert.Equal(50, config.MobAi.InvocationCapMs);
        Assert.Equal(3, config.MobAi.QuarantineStrikes);
    }

    [Fact]
    public void Tick_SweepsRoomContents_NotTagIndex()
    {
        var world = new World();
        var room = new Room("core:town-square", "Town Square", "A square.");
        world.AddRoom(room);

        var mobEntity = new Entity("npc", "a goblin");
        mobEntity.SetProperty("behavior", "test-behavior");
        mobEntity.LocationRoomId = "core:town-square";
        room.AddEntity(mobEntity);
        // Deliberately NOT world.TrackEntity / AddTag / SwapTagBuffers:
        // the sweep must come from room contents, not the tag index.

        var manager = BuildManager(world);
        var handlerCalled = false;
        manager.RegisterBehavior("test-behavior", (_) => { handlerCalled = true; });

        manager.PlayerEnteredRoom("core:town-square");
        manager.Tick();

        Assert.True(handlerCalled);
    }

    [Fact]
    public void Tick_BudgetExhausted_DefersMobs_AndCursorResumes()
    {
        var world = new World();
        var room = new Room("core:town-square", "Town Square", "A square.");
        world.AddRoom(room);
        AddMob(world, room, "slow", "goblin-a");
        AddMob(world, room, "slow", "goblin-b");
        AddMob(world, room, "slow", "goblin-c");

        var time = new FakeTime();
        var config = new Tapestry.Data.ServerConfig(); // tick budget 25ms
        var manager = BuildManager(world, config, time);

        var calls = new List<string>();
        manager.RegisterBehavior("slow", mob =>
        {
            calls.Add(mob.Name);
            time.AdvanceMs(30); // each mob costs 30ms > 25ms budget
        });
        manager.PlayerEnteredRoom("core:town-square");

        manager.Tick();
        Assert.Single(calls);                  // budget allows exactly one 30ms mob
        Assert.Equal(2, manager.CurrentCursorLag);

        manager.Tick();
        manager.Tick();
        Assert.Equal(3, calls.Count);
        Assert.Equal(3, calls.Distinct().Count()); // cursor visited each mob exactly once
    }

    [Fact]
    public void Tick_WithinBudget_ProcessesAllMobs_NoLag()
    {
        var world = new World();
        var room = new Room("core:town-square", "Town Square", "A square.");
        world.AddRoom(room);
        AddMob(world, room, "cheap", "goblin-a");
        AddMob(world, room, "cheap", "goblin-b");
        AddMob(world, room, "cheap", "goblin-c");

        var time = new FakeTime();
        var manager = BuildManager(world, new Tapestry.Data.ServerConfig(), time);

        var calls = 0;
        manager.RegisterBehavior("cheap", _ => { calls++; time.AdvanceMs(1); });
        manager.PlayerEnteredRoom("core:town-square");

        manager.Tick();

        Assert.Equal(3, calls);
        Assert.Equal(0, manager.CurrentCursorLag);
    }

    [Fact]
    public void Tick_AlwaysProcessesAtLeastOneMob_EvenOverBudget()
    {
        var world = new World();
        var room = new Room("core:town-square", "Town Square", "A square.");
        world.AddRoom(room);
        AddMob(world, room, "glacial", "goblin-a");
        AddMob(world, room, "glacial", "goblin-b");

        var time = new FakeTime();
        var manager = BuildManager(world, new Tapestry.Data.ServerConfig(), time);

        var calls = 0;
        manager.RegisterBehavior("glacial", _ => { calls++; time.AdvanceMs(500); });
        manager.PlayerEnteredRoom("core:town-square");

        manager.Tick();
        Assert.Equal(1, calls); // min-progress guarantee: never zero
        manager.Tick();
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Tick_CursorToleratesDespawnBetweenTicks()
    {
        var world = new World();
        var room = new Room("core:town-square", "Town Square", "A square.");
        world.AddRoom(room);
        AddMob(world, room, "slow", "goblin-a");
        var victim = AddMob(world, room, "slow", "goblin-b");
        AddMob(world, room, "slow", "goblin-c");

        var time = new FakeTime();
        var manager = BuildManager(world, new Tapestry.Data.ServerConfig(), time);

        var calls = new List<string>();
        manager.RegisterBehavior("slow", mob => { calls.Add(mob.Name); time.AdvanceMs(30); });
        manager.PlayerEnteredRoom("core:town-square");

        manager.Tick(); // processes exactly one mob
        room.RemoveEntity(victim);
        world.UntrackEntity(victim);

        manager.Tick();
        manager.Tick();

        // No crash, no double-visit; the two survivors plus the first-processed mob
        // were each visited at least once and the despawned mob at most once.
        Assert.True(calls.Count >= 3);
        Assert.True(calls.Count(n => n == victim.Name) <= 1);
    }
}
