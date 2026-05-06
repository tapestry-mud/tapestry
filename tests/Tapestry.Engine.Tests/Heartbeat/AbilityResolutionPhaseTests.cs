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

    [Fact]
    public void Resolve_Variance0_AlwaysHitsRegardlessOfLowProficiency()
    {
        Setup();
        var player = CreatePlayer("Alice", 100, 100);
        var mob = CreateMob("Trolloc", 50);
        _combatManager.Engage(player, mob);

        _abilityRegistry.Register(new AbilityDefinition
        {
            Id = "test_zero_variance",
            Name = "Test Zero Variance",
            Category = AbilityCategory.Skill,
            Variance = 0,
            ProficiencyGainChance = 0.0
        });

        _proficiencyManager.Learn(player.Id, "test_zero_variance", 1);

        // Run 20 times -- all should hit (ability.used), never miss
        for (var i = 0; i < 20; i++)
        {
            _events.Clear();
            player.SetProperty("queued_actions", new List<object>
            {
                new Dictionary<string, object?> { ["abilityId"] = "test_zero_variance" }
            });
            _phase.Execute(MakeContext(i + 1));
        }

        Assert.DoesNotContain(_events, e => e.Type == "ability.missed");
    }

    [Fact]
    public void Resolve_Variance50_HalfProficiencyChance()
    {
        Setup();
        var player = CreatePlayer("Bob", 100, 100);
        player.Stats.BaseLuck = 0;
        var mob = CreateMob("Trolloc", 5000);
        _combatManager.Engage(player, mob);

        _abilityRegistry.Register(new AbilityDefinition
        {
            Id = "test_half_variance",
            Name = "Test Half Variance",
            Type = AbilityType.Active,
            Category = AbilityCategory.Skill,
            Variance = 50,
            ProficiencyGainChance = 0.0,
            MaxChance = 100
        });

        _proficiencyManager.Learn(player.Id, "test_half_variance", 100);

        // proficiency=100, variance=50, luck=0 -> hitChance=50 -> ~50% hit rate
        var hitCount = 0;
        for (var i = 0; i < 200; i++)
        {
            _events.Clear();
            QueueAbility(player, "test_half_variance");

            // Create context with varied seed for each iteration
            var ctx = new PulseContext
            {
                CurrentTick = i + 1,
                CurrentPulse = i + 1,
                World = _world,
                EventBus = _eventBus,
                CombatManager = _combatManager,
                AbilityRegistry = _abilityRegistry,
                ProficiencyManager = _proficiencyManager,
                EffectManager = _effectManager,
                SessionManager = _sessionManager,
                Random = new Random(i + 1000)
            };
            _phase.Execute(ctx);

            if (_events.Any(e => e.Type == "ability.used")) { hitCount++; }
        }

        // ~50% hit rate -- allow 60-140 out of 200
        Assert.InRange(hitCount, 60, 140);
    }

    [Fact]
    public void Resolve_LuckBonus_IncreasesHitChance()
    {
        Setup();
        var phase = new AbilityResolutionPhase(luckScale: 1.0); // luck=1 = +1% per luck point

        var player = CreatePlayer("Charlie", 100, 100);
        player.Stats.BaseLuck = 0;
        var mob = CreateMob("Trolloc", 5000);
        _combatManager.Engage(player, mob);

        _abilityRegistry.Register(new AbilityDefinition
        {
            Id = "test_luck_ability",
            Name = "Test Luck",
            Category = AbilityCategory.Skill,
            Variance = 100,
            ProficiencyGainChance = 0.0
        });

        _proficiencyManager.Learn(player.Id, "test_luck_ability", 0);
        _proficiencyManager.SetProficiency(player.Id, "test_luck_ability", 1);

        // With proficiency=1, variance=100, luck=0: hitChance=1 -> very low hit rate
        var hitCount = 0;
        for (var i = 0; i < 100; i++)
        {
            player.SetProperty("queued_actions", new List<object>
            {
                new Dictionary<string, object?> { ["abilityId"] = "test_luck_ability" }
            });
            var ctx = new PulseContext
            {
                CurrentTick = i + 1,
                CurrentPulse = i + 1,
                World = _world,
                EventBus = _eventBus,
                CombatManager = _combatManager,
                AbilityRegistry = _abilityRegistry,
                ProficiencyManager = _proficiencyManager,
                EffectManager = _effectManager,
                SessionManager = _sessionManager,
                AlignmentManager = new Tapestry.Engine.Alignment.AlignmentManager(_world, _eventBus, new Tapestry.Engine.Alignment.AlignmentConfig()),
                Random = new Random(i + 100)
            };
            phase.Execute(ctx);
            if (_events.Any(e => e.Type == "ability.used")) { hitCount++; }
            _events.Clear();
        }

        // With hitChance=1, expect roughly 1% hit rate -- definitely < 10 out of 100
        Assert.True(hitCount < 10, $"Expected <10 hits with hitChance=1, got {hitCount}");
    }
}
