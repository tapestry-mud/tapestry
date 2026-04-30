using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Heartbeat;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Heartbeat;

public class AbilityResolutionPhaseTests
{
    private World _world = null!;
    private EventBus _eventBus = null!;
    private AbilityRegistry _abilityRegistry = null!;
    private ProficiencyManager _proficiencyManager = null!;
    private EffectManager _effectManager = null!;
    private SessionManager _sessionManager = null!;
    private Room _room = null!;
    private CombatManager _combatManager = null!;
    private AbilityResolutionPhase _phase = null!;
    private List<GameEvent> _events = null!;

    private void Setup()
    {
        _world = new World();
        _eventBus = new EventBus();
        _abilityRegistry = new AbilityRegistry();
        _proficiencyManager = new ProficiencyManager(_world, _abilityRegistry);
        _effectManager = new EffectManager(_world, _eventBus);
        _sessionManager = new SessionManager();
        _room = new Room("test-room", "Test Room", "A test room.");
        _world.AddRoom(_room);
        _combatManager = new CombatManager(_world, _eventBus);
        _phase = new AbilityResolutionPhase();
        _events = new List<GameEvent>();
        _eventBus.Subscribe("*", e => { _events.Add(e); });
    }

    private Entity CreatePlayer(string name, int resource, int movement)
    {
        var entity = new Entity("player", name);
        entity.Stats.BaseMaxResource = resource;
        entity.Stats.Resource = resource;
        entity.Stats.BaseMaxMovement = movement;
        entity.Stats.Movement = movement;
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.Hp = 100;
        _room.AddEntity(entity);
        _world.TrackEntity(entity);
        return entity;
    }

    private Entity CreateMob(string name, int hp)
    {
        var entity = new Entity("npc", name);
        entity.Stats.BaseMaxHp = hp;
        entity.Stats.Hp = hp;
        entity.AddTag("killable");
        _room.AddEntity(entity);
        _world.TrackEntity(entity);
        return entity;
    }

    private PulseContext MakeContext(long pulse)
    {
        return new PulseContext
        {
            CurrentTick = pulse,
            CurrentPulse = pulse,
            World = _world,
            EventBus = _eventBus,
            CombatManager = _combatManager,
            AbilityRegistry = _abilityRegistry,
            ProficiencyManager = _proficiencyManager,
            EffectManager = _effectManager,
            SessionManager = _sessionManager,
            Random = new Random(42)
        };
    }

    private void RegisterKick()
    {
        _abilityRegistry.Register(new AbilityDefinition
        {
            Id = "kick",
            Name = "Kick",
            Type = AbilityType.Active,
            Category = AbilityCategory.Skill,
            ResourceCost = 10,
            PulseDelay = 0,
            MaxChance = 100
        });
    }

    private void QueueAbility(Entity entity, string abilityId, Guid? targetId = null)
    {
        var queue = entity.GetProperty<List<object>>(AbilityProperties.QueuedActions);
        if (queue == null)
        {
            queue = new List<object>();
            entity.SetProperty(AbilityProperties.QueuedActions, queue);
        }

        var entry = new Dictionary<string, object?>
        {
            ["abilityId"] = abilityId,
            ["targetEntityId"] = targetId
        };
        queue.Add(entry);
    }

    [Fact]
    public void Execute_ProcessesQueuedAbility_DeductsResources()
    {
        Setup();
        RegisterKick();
        var player = CreatePlayer("TestPlayer", 100, 50);
        var mob = CreateMob("Goblin", 50);

        _proficiencyManager.Learn(player.Id, "kick", 95);
        _combatManager.Engage(player, mob);
        QueueAbility(player, "kick", mob.Id);

        _phase.Execute(MakeContext(1));

        Assert.Equal(40, player.Stats.Movement);
        var usedOrMissed = _events.Any(e => e.Type == "ability.used" || e.Type == "ability.missed");
        Assert.True(usedOrMissed);
    }

    [Fact]
    public void Execute_FifoQueue_ProcessesFirstItem()
    {
        Setup();
        RegisterKick();

        // Register bash too
        _abilityRegistry.Register(new AbilityDefinition
        {
            Id = "bash",
            Name = "Bash",
            Type = AbilityType.Active,
            Category = AbilityCategory.Skill,
            ResourceCost = 15,
            PulseDelay = 0,
            MaxChance = 100
        });

        var player = CreatePlayer("TestPlayer", 100, 100);
        var mob = CreateMob("Goblin", 50);

        _proficiencyManager.Learn(player.Id, "kick", 95);
        _proficiencyManager.Learn(player.Id, "bash", 95);
        _combatManager.Engage(player, mob);

        QueueAbility(player, "kick", mob.Id);
        QueueAbility(player, "bash", mob.Id);

        _phase.Execute(MakeContext(1));

        // Kick fired (10 cost), bash still in queue
        Assert.Equal(90, player.Stats.Movement);

        var queue = player.GetProperty<List<object>>(AbilityProperties.QueuedActions);
        Assert.NotNull(queue);
        Assert.Single(queue);

        var remaining = queue![0] as Dictionary<string, object?>;
        Assert.Equal("bash", remaining!["abilityId"]);
    }

    [Fact]
    public void Execute_FizzleFlush_SkipsFizzlesAndExecutesFirstValid()
    {
        Setup();
        RegisterKick();

        // Register heal (spell, self-target, no combat required)
        _abilityRegistry.Register(new AbilityDefinition
        {
            Id = "heal",
            Name = "Heal",
            Type = AbilityType.Active,
            Category = AbilityCategory.Spell,
            ResourceCost = 10,
            PulseDelay = 0,
            MaxChance = 100,
            Metadata = new Dictionary<string, object?> { ["heal_dice"] = "2d4" }
        });

        var player = CreatePlayer("TestPlayer", 100, 100);
        _proficiencyManager.Learn(player.Id, "kick", 95);
        _proficiencyManager.Learn(player.Id, "heal", 95);

        // Two kicks (will fizzle: not in combat, offensive ability)
        QueueAbility(player, "kick");
        QueueAbility(player, "kick");
        // Then heal (should succeed: non-offensive, no combat required)
        QueueAbility(player, "heal");

        _phase.Execute(MakeContext(1));

        // Kicks fizzled (no resource cost), heal fired (10 resource cost)
        Assert.Equal(100, player.Stats.Movement); // kicks didn't fire
        Assert.Equal(90, player.Stats.Resource);   // heal cost 10

        var fizzles = _events.Where(e => e.Type == "ability.fizzled").ToList();
        Assert.Equal(2, fizzles.Count);

        var healEvent = _events.Any(e =>
            (e.Type == "ability.used" || e.Type == "ability.missed") &&
            (string?)e.Data["abilityId"] == "heal");
        Assert.True(healEvent);
    }

    [Fact]
    public void Execute_EffectGate_FizzlesWhenEffectPresent()
    {
        Setup();

        // Register sanctuary spell with effect
        _abilityRegistry.Register(new AbilityDefinition
        {
            Id = "sanctuary",
            Name = "Sanctuary",
            Type = AbilityType.Active,
            Category = AbilityCategory.Spell,
            ResourceCost = 20,
            PulseDelay = 0,
            MaxChance = 100,
            Effect = new AbilityEffectDefinition
            {
                EffectId = "sanctuary",
                DurationPulses = 10,
                Flags = new List<string> { "sanctuary" }
            }
        });

        var player = CreatePlayer("TestPlayer", 100, 100);
        _proficiencyManager.Learn(player.Id, "sanctuary", 95);

        // Already have sanctuary effect active
        _effectManager.TryApply(new ActiveEffect
        {
            Id = "sanctuary",
            SourceEntityId = player.Id,
            TargetEntityId = player.Id,
            RemainingPulses = 5
        });

        QueueAbility(player, "sanctuary");

        var startResource = player.Stats.Resource;
        _phase.Execute(MakeContext(1));

        // No resource cost on fizzle
        Assert.Equal(startResource, player.Stats.Resource);

        var fizzle = _events.FirstOrDefault(e =>
            e.Type == "ability.fizzled" &&
            (string?)e.Data["reason"] == "effect_present");
        Assert.NotNull(fizzle);
    }

    [Fact]
    public void Execute_InitiateOnly_FizzlesWhenInCombat()
    {
        Setup();

        _abilityRegistry.Register(new AbilityDefinition
        {
            Id = "backstab",
            Name = "Backstab",
            Type = AbilityType.Active,
            Category = AbilityCategory.Skill,
            ResourceCost = 15,
            PulseDelay = 0,
            MaxChance = 100,
            InitiateOnly = true
        });

        var player = CreatePlayer("TestPlayer", 100, 100);
        var mob = CreateMob("Goblin", 50);

        _proficiencyManager.Learn(player.Id, "backstab", 95);
        _combatManager.Engage(player, mob);

        QueueAbility(player, "backstab", mob.Id);

        _phase.Execute(MakeContext(1));

        // No resource cost on fizzle
        Assert.Equal(100, player.Stats.Movement);

        var fizzle = _events.FirstOrDefault(e =>
            e.Type == "ability.fizzled" &&
            (string?)e.Data["reason"] == "initiate_only");
        Assert.NotNull(fizzle);
    }

    [Fact]
    public void Execute_SetsLastAbilityUsed()
    {
        Setup();
        RegisterKick();
        var player = CreatePlayer("TestPlayer", 100, 50);
        var mob = CreateMob("Goblin", 50);

        _proficiencyManager.Learn(player.Id, "kick", 95);
        _combatManager.Engage(player, mob);
        QueueAbility(player, "kick", mob.Id);

        _phase.Execute(MakeContext(1));

        var lastUsed = player.GetProperty<string>(AbilityProperties.LastAbilityUsed);
        Assert.Equal("kick", lastUsed);
    }

    [Fact]
    public void Execute_PulseDelay_FizzlesWhenNotReady()
    {
        Setup();

        _abilityRegistry.Register(new AbilityDefinition
        {
            Id = "fireball",
            Name = "Fireball",
            Type = AbilityType.Active,
            Category = AbilityCategory.Spell,
            ResourceCost = 20,
            PulseDelay = 1,
            MaxChance = 100,
            Metadata = new Dictionary<string, object?> { ["damage_dice"] = "3d6" }
        });

        var player = CreatePlayer("TestPlayer", 200, 100);
        var mob = CreateMob("Goblin", 50);

        _proficiencyManager.Learn(player.Id, "fireball", 95);
        _combatManager.Engage(player, mob);

        // Pulse 1: fires (no previous delay)
        QueueAbility(player, "fireball", mob.Id);
        _phase.Execute(MakeContext(1));
        var pulse1Resource = player.Stats.Resource;
        Assert.Equal(180, pulse1Resource); // cost 20

        // Pulse 2: should fizzle (delay not ready)
        QueueAbility(player, "fireball", mob.Id);
        _phase.Execute(MakeContext(2));
        Assert.Equal(180, player.Stats.Resource); // no cost, fizzled

        var delayFizzle = _events.Any(e =>
            e.Type == "ability.fizzled" &&
            (string?)e.Data["reason"] == "pulse_delay");
        Assert.True(delayFizzle);

        // Pulse 3: should fire again (delay expired)
        QueueAbility(player, "fireball", mob.Id);
        _phase.Execute(MakeContext(3));
        Assert.Equal(160, player.Stats.Resource); // cost 20 again
    }

    [Fact]
    public void Execute_NonCombatEntity_ProcessesHeal()
    {
        Setup();

        _abilityRegistry.Register(new AbilityDefinition
        {
            Id = "heal",
            Name = "Heal",
            Type = AbilityType.Active,
            Category = AbilityCategory.Spell,
            ResourceCost = 15,
            PulseDelay = 0,
            MaxChance = 100,
            Metadata = new Dictionary<string, object?> { ["heal_dice"] = "2d8" }
        });

        var player = CreatePlayer("TestPlayer", 100, 50);
        _proficiencyManager.Learn(player.Id, "heal", 95);

        // Not in combat
        QueueAbility(player, "heal");

        _phase.Execute(MakeContext(1));

        // Resources deducted
        Assert.Equal(85, player.Stats.Resource);

        // Ability used or missed (proficiency check)
        var healEvent = _events.Any(e =>
            (e.Type == "ability.used" || e.Type == "ability.missed") &&
            (string?)e.Data["abilityId"] == "heal");
        Assert.True(healEvent);

        // Queue cleared
        Assert.False(player.HasProperty(AbilityProperties.QueuedActions));
    }
}
