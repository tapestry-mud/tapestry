// tests/Tapestry.Engine.Tests/Mobs/MobAIManagerTests.cs
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Mobs;

namespace Tapestry.Engine.Tests.Mobs;

public class MobAIManagerTests
{
    private static MobAIManager BuildManager(World world)
    {
        var eventBus = new EventBus();
        var combatManager = new CombatManager(world, eventBus);
        var alignmentConfig = new AlignmentConfig();
        var alignmentManager = new AlignmentManager(world, eventBus, alignmentConfig);
        var dispositionEvaluator = new DispositionEvaluator(world, eventBus, alignmentManager);
        return new MobAIManager(world, eventBus, combatManager, dispositionEvaluator,
            NullLogger<MobAIManager>.Instance);
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
            NullLogger<MobAIManager>.Instance);

        var room = new Room("core:town-square", "Town Square", "A square.");
        world.AddRoom(room);

        var mob = new Entity("npc", "Guard");
        mob.LocationRoomId = "core:town-square";
        mob.SetProperty("behavior", "stationary");
        mob.AddTag("npc");
        room.AddEntity(mob);
        world.TrackEntity(mob);

        manager.RegisterBehavior("stationary", _ => { });
        manager.PlayerEnteredRoom("core:town-square");

        var received = new List<Tapestry.Shared.GameEvent>();
        eventBus.Subscribe("mob.ai.tick", evt => { received.Add(evt); });

        manager.Tick();

        Assert.Single(received);
        Assert.Equal(mob.Id.ToString(), received[0].Data["entityId"]);
        Assert.Equal("core:town-square", received[0].Data["roomId"]);
    }
}
