// tests/Tapestry.Engine.Tests/Combat/CombatManagerTests.cs
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Heartbeat;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Combat;

public class CombatManagerTests
{
    private World _world = null!;
    private EventBus _eventBus = null!;
    private CombatManager _combat = null!;
    private Room _room = null!;

    private void Setup()
    {
        _world = new World();
        _eventBus = new EventBus();
        _combat = new CombatManager(_world, _eventBus);
        _room = new Room("core:arena", "Arena", "A test arena.");
        _world.AddRoom(_room);
    }

    private Entity CreatePlayer(string name = "Travis", int hp = 100)
    {
        var entity = new Entity("player", name);
        entity.AddTag("player");
        entity.Stats.BaseMaxHp = hp;
        entity.Stats.Hp = hp;
        entity.Stats.BaseStrength = 10;
        entity.Stats.BaseDexterity = 10;
        _room.AddEntity(entity);
        _world.TrackEntity(entity);
        return entity;
    }

    private Entity CreateMob(string name = "a goblin", int hp = 40)
    {
        var entity = new Entity("npc", name);
        entity.AddTag("npc");
        entity.AddTag("killable");
        entity.Stats.BaseMaxHp = hp;
        entity.Stats.Hp = hp;
        entity.Stats.BaseStrength = 8;
        entity.Stats.BaseDexterity = 10;
        entity.SetProperty("level", 3);
        _room.AddEntity(entity);
        _world.TrackEntity(entity);
        return entity;
    }

    [Fact]
    public void Engage_AddsToCombatLists()
    {
        Setup();
        var player = CreatePlayer();
        var mob = CreateMob();
        var result = _combat.Engage(player, mob);
        Assert.True(result);
        Assert.True(_combat.IsInCombat(player.Id));
        Assert.True(_combat.IsInCombat(mob.Id));
    }

    [Fact]
    public void Engage_SetsPrimaryTargets()
    {
        Setup();
        var player = CreatePlayer();
        var mob = CreateMob();
        _combat.Engage(player, mob);
        Assert.Equal(mob.Id, _combat.GetPrimaryTarget(player.Id));
        Assert.Equal(player.Id, _combat.GetPrimaryTarget(mob.Id));
    }

    [Fact]
    public void Engage_SecondAttacker_MobKeepsPrimaryTarget()
    {
        Setup();
        var player1 = CreatePlayer("Travis");
        var player2 = CreatePlayer("Ava");
        var mob = CreateMob();
        _combat.Engage(player1, mob);
        _combat.Engage(player2, mob);
        Assert.Equal(player1.Id, _combat.GetPrimaryTarget(mob.Id));
        Assert.Equal(mob.Id, _combat.GetPrimaryTarget(player1.Id));
        Assert.Equal(mob.Id, _combat.GetPrimaryTarget(player2.Id));
    }

    [Fact]
    public void Engage_RejectsNonKillableTarget()
    {
        Setup();
        var player = CreatePlayer();
        var vendor = new Entity("npc", "blacksmith");
        vendor.AddTag("npc");
        vendor.AddTag("no-kill");
        _room.AddEntity(vendor);
        var result = _combat.Engage(player, vendor);
        Assert.False(result);
        Assert.False(_combat.IsInCombat(player.Id));
    }

    [Fact]
    public void Engage_RejectsInSafeRoom()
    {
        Setup();
        _room.AddTag("safe");
        var player = CreatePlayer();
        var mob = CreateMob();
        var result = _combat.Engage(player, mob);
        Assert.False(result);
    }

    [Fact]
    public void Engage_RejectsInNoCombatRoom()
    {
        Setup();
        _room.AddTag("no-combat");
        var player = CreatePlayer();
        var mob = CreateMob();
        var result = _combat.Engage(player, mob);
        Assert.False(result);
    }

    [Fact]
    public void RemoveFromCombat_CleansUpBothSides()
    {
        Setup();
        var player = CreatePlayer();
        var mob = CreateMob();
        _combat.Engage(player, mob);
        _combat.RemoveFromCombat(player.Id, mob.Id);
        Assert.False(_combat.IsInCombat(player.Id));
        Assert.False(_combat.IsInCombat(mob.Id));
    }

    [Fact]
    public void RemoveEntityFromAllCombat_CleansUpEverything()
    {
        Setup();
        var player1 = CreatePlayer("Travis");
        var player2 = CreatePlayer("Ava");
        var mob = CreateMob();
        _combat.Engage(player1, mob);
        _combat.Engage(player2, mob);
        _combat.RemoveEntityFromAllCombat(mob.Id);
        Assert.False(_combat.IsInCombat(mob.Id));
        Assert.False(_combat.IsInCombat(player1.Id));
        Assert.False(_combat.IsInCombat(player2.Id));
    }

    [Fact]
    public void Retarget_OnPrimaryTargetRemoved_ShiftsToNext()
    {
        Setup();
        var player1 = CreatePlayer("Travis");
        var player2 = CreatePlayer("Ava");
        var mob = CreateMob();
        _combat.Engage(player1, mob);
        _combat.Engage(player2, mob);
        _combat.RemoveEntityFromAllCombat(player1.Id);
        Assert.Equal(player2.Id, _combat.GetPrimaryTarget(mob.Id));
        Assert.True(_combat.IsInCombat(mob.Id));
    }

    [Fact]
    public void GetPrimaryTarget_NoCombat_ReturnsNull()
    {
        Setup();
        var player = CreatePlayer();
        Assert.Null(_combat.GetPrimaryTarget(player.Id));
    }

    [Fact]
    public void GetCombatants_ReturnsAllInCombat()
    {
        Setup();
        var player = CreatePlayer();
        var mob = CreateMob();
        _combat.Engage(player, mob);
        var combatants = _combat.GetCombatants();
        Assert.Contains(combatants, e => e.Id == player.Id);
        Assert.Contains(combatants, e => e.Id == mob.Id);
    }

    [Fact]
    public void AttemptFlee_MovesToRandomExit()
    {
        Setup();
        var exitRoom = new Room("core:hallway", "Hallway", "A hallway.");
        _world.AddRoom(exitRoom);
        _room.SetExit(Direction.North, new Exit("core:hallway"));
        var player = CreatePlayer();
        var mob = CreateMob();
        _combat.Engage(player, mob);
        var context = new PulseContext
        {
            CurrentTick = 100,
            CurrentPulse = 100,
            World = _world,
            EventBus = _eventBus,
            CombatManager = _combat,
            EffectManager = new EffectManager(_world, _eventBus),
            Random = new Random(42)
        };
        var result = _combat.AttemptFlee(player, context);
        Assert.True(result);
        Assert.False(_combat.IsInCombat(player.Id));
        Assert.Equal("core:hallway", player.LocationRoomId);
    }

    [Fact]
    public void AttemptFlee_NoExits_Fails()
    {
        Setup();
        var player = CreatePlayer();
        var mob = CreateMob();
        _combat.Engage(player, mob);
        var context = new PulseContext
        {
            CurrentTick = 100,
            CurrentPulse = 100,
            World = _world,
            EventBus = _eventBus,
            CombatManager = _combat,
            EffectManager = new EffectManager(_world, _eventBus),
            Random = new Random(42)
        };
        var result = _combat.AttemptFlee(player, context);
        Assert.False(result);
        Assert.True(_combat.IsInCombat(player.Id));
    }

    [Fact]
    public void AttemptFlee_NoFleeTag_Prevented()
    {
        Setup();
        var exitRoom = new Room("core:hallway", "Hallway", "A hallway.");
        _world.AddRoom(exitRoom);
        _room.SetExit(Direction.North, new Exit("core:hallway"));
        var player = CreatePlayer();
        player.AddTag("no_flee");
        var mob = CreateMob();
        _combat.Engage(player, mob);
        var context = new PulseContext
        {
            CurrentTick = 100,
            CurrentPulse = 100,
            World = _world,
            EventBus = _eventBus,
            CombatManager = _combat,
            EffectManager = new EffectManager(_world, _eventBus),
            Random = new Random(42)
        };
        var result = _combat.AttemptFlee(player, context);
        Assert.False(result);
        Assert.True(_combat.IsInCombat(player.Id));
    }

    [Fact]
    public void AttemptFlee_SetsFleeCooldown()
    {
        Setup();
        var exitRoom = new Room("core:hallway", "Hallway", "A hallway.");
        _world.AddRoom(exitRoom);
        _room.SetExit(Direction.North, new Exit("core:hallway"));
        var player = CreatePlayer();
        var mob = CreateMob();
        _combat.Engage(player, mob);
        var context = new PulseContext
        {
            CurrentTick = 100,
            CurrentPulse = 100,
            World = _world,
            EventBus = _eventBus,
            CombatManager = _combat,
            EffectManager = new EffectManager(_world, _eventBus),
            Random = new Random(42)
        };
        _combat.AttemptFlee(player, context);
        Assert.True(_combat.HasFleeCooldown(player.Id, 100));
        Assert.False(_combat.HasFleeCooldown(player.Id, 200));
    }

    [Fact]
    public void Engage_RejectsDuringFleeCooldown()
    {
        Setup();
        var exitRoom = new Room("core:hallway", "Hallway", "A hallway.");
        _world.AddRoom(exitRoom);
        _room.SetExit(Direction.North, new Exit("core:hallway"));
        var player = CreatePlayer();
        var mob = CreateMob();
        _combat.Engage(player, mob);
        var context = new PulseContext
        {
            CurrentTick = 100,
            CurrentPulse = 100,
            World = _world,
            EventBus = _eventBus,
            CombatManager = _combat,
            EffectManager = new EffectManager(_world, _eventBus),
            Random = new Random(42)
        };
        _combat.AttemptFlee(player, context);
        var arena = _world.GetRoom("core:arena")!;
        arena.AddEntity(player);
        var result = _combat.Engage(player, mob, currentTick: 100);
        Assert.False(result);
    }

    // Tick_ExecutesPhasesInOrder — removed: CombatManager.Tick() removed (dead code, heartbeat handles combat pulse).

    [Fact]
    public void HandleEntityDeath_PublishesKillEventWithRoomId()
    {
        Setup();
        var player = CreatePlayer();
        var mob = CreateMob();
        _combat.Engage(player, mob);

        GameEvent? killEvent = null;
        _eventBus.Subscribe("combat.kill", evt => { killEvent = evt; });

        _combat.HandleEntityDeath(mob.Id, player.Id);

        Assert.NotNull(killEvent);
        Assert.Equal("core:arena", killEvent.RoomId);
        Assert.Equal(player.Name, killEvent.SourceEntityName);
    }

}
