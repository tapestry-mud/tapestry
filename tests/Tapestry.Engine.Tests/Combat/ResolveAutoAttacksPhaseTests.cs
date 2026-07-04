using Tapestry.Engine.Abilities;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Heartbeat;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Combat;

public class ResolveAutoAttacksPhaseTests
{
    private World _world = null!;
    private EventBus _eventBus = null!;
    private CombatManager _combat = null!;
    private WindowValidatorRegistry _validators = null!;
    private Room _room = null!;

    private SwellClockManager Setup()
    {
        _world = new World();
        _eventBus = new EventBus();
        _combat = new CombatManager(_world, _eventBus, vitalsService: new VitalsService(_eventBus));
        _validators = new WindowValidatorRegistry();
        _room = new Room("core:arena", "Arena", "A test arena.");
        _world.AddRoom(_room);
        _validators.Register("telegraph-rung", ctx =>
        {
            if (string.IsNullOrEmpty(ctx.Command.Verb))
            {
                return new ValidationResult { Outcome = WindowOutcome.Weathered, NarrationKey = "weathered" };
            }
            if (ctx.Command.Verb == ctx.Swell!.RequiredCounter)
            {
                return new ValidationResult { Outcome = WindowOutcome.Countered, NarrationKey = "countered" };
            }
            return new ValidationResult { Outcome = WindowOutcome.Whiffed, NarrationKey = "whiffed" };
        });
        return new SwellClockManager(_world, _eventBus, _combat, _validators, vitalsService: new VitalsService(_eventBus));
    }

    private Entity CreatePlayer(int hp = 100)
    {
        var entity = new Entity("player", "Travis");
        entity.AddTag("player");
        entity.Stats.BaseMaxHp = hp;
        entity.Stats.Hp = hp;
        entity.Stats.BaseStrength = 10;
        entity.Stats.BaseDexterity = 10;
        _room.AddEntity(entity);
        _world.TrackEntity(entity);
        return entity;
    }

    private Entity CreateSwellBoss(int hp = 200)
    {
        var entity = new Entity("npc", "the swell-warden");
        entity.AddTag("npc");
        entity.Stats.BaseMaxHp = hp;
        entity.Stats.Hp = hp;
        entity.Stats.BaseStrength = 8;
        entity.Stats.BaseDexterity = 10;
        // Dials: fast baseline (gap 2 ticks, no jitter for determinism), short telegraph + window.
        entity.SetProperty("swell_window", "telegraph-rung");
        entity.SetProperty("swell_tell", "full");
        entity.SetProperty("swell_baseline_gap_ticks", 2);
        entity.SetProperty("swell_jitter_ticks", 0);
        entity.SetProperty("swell_telegraph_ticks", 2);
        entity.SetProperty("swell_window_ticks", 4);
        entity.SetProperty("swell_line1_id", "sweep");
        entity.SetProperty("swell_line1_counter", "sidestep");
        entity.SetProperty("swell_line1_tell_full", "The warden winds up a wide SWEEP -- sidestep it!");
        entity.SetProperty("swell_line2_id", "crush");
        entity.SetProperty("swell_line2_counter", "brace");
        entity.SetProperty("swell_line2_tell_full", "The warden rears for a downward CRUSH -- brace for it!");
        entity.SetProperty("swell_chunk_pct", 15);
        entity.SetProperty("swell_whiff_pct", 10);
        entity.SetProperty("swell_weather_pct", 8);
        entity.SetProperty("swell_narration_countered", "You read it clean and the warden reels.");
        entity.SetProperty("swell_narration_whiffed", "Wrong read - the blow lands hard.");
        entity.SetProperty("swell_narration_weathered", "You freeze, and the blow lands.");
        _room.AddEntity(entity);
        _world.TrackEntity(entity);
        return entity;
    }

    private PulseContext BuildContext(SwellClockManager manager)
    {
        var abilityRegistry = new AbilityRegistry();
        var proficiency = new ProficiencyManager(_world, abilityRegistry);
        var passive = new PassiveAbilityProcessor(abilityRegistry, proficiency);
        return new PulseContext
        {
            CurrentTick = 0,
            World = _world,
            EventBus = _eventBus,
            CombatManager = _combat,
            SwellClockManager = manager,
            AbilityRegistry = abilityRegistry,
            ProficiencyManager = proficiency,
            PassiveAbilityProcessor = passive,
            SessionManager = new SessionManager()
        };
    }

    [Fact]
    public void Execute_SkipsAutoAttack_WhileFightIsSwelling()
    {
        var manager = Setup();
        var player = CreatePlayer();
        var boss = CreateSwellBoss();
        _combat.Engage(player, boss);

        // Drive to Baseline tick 0 (PhaseEndsAtTick = 2), then tick 2 to enter Telegraph.
        manager.AdvanceAll(0); // Baseline registered
        manager.AdvanceAll(2); // -> Telegraph (phase is now non-Baseline)

        // Subscribe to combat.hit events BEFORE calling Execute.
        var hitEvents = new List<GameEvent>();
        _eventBus.Subscribe("combat.hit", e => hitEvents.Add(e));

        var phase = new ResolveAutoAttacksPhase();
        var context = BuildContext(manager);
        phase.Execute(context);

        // Both directions (player attacks boss, boss attacks player) must be suppressed.
        Assert.DoesNotContain(hitEvents, e => e.Type == "combat.hit");
    }

    [Fact]
    public void Execute_Hit_PublishesEntityVitalChanged()
    {
        _world = new World();
        _eventBus = new EventBus();
        _combat = new CombatManager(_world, _eventBus, vitalsService: new VitalsService(_eventBus));
        _room = new Room("core:arena", "Arena", "A test arena.");
        _world.AddRoom(_room);

        var player = CreatePlayer();
        var boss = CreateSwellBoss();
        _combat.Engage(player, boss);

        var abilityRegistry = new AbilityRegistry();
        var proficiency = new ProficiencyManager(_world, abilityRegistry);
        var passive = new PassiveAbilityProcessor(abilityRegistry, proficiency);
        var context = new PulseContext
        {
            CurrentTick = 0,
            World = _world,
            EventBus = _eventBus,
            CombatManager = _combat,
            AbilityRegistry = abilityRegistry,
            ProficiencyManager = proficiency,
            PassiveAbilityProcessor = passive,
            SessionManager = new SessionManager(),
            VitalsService = new VitalsService(_eventBus),
            Random = new AlwaysHitRandom()
        };

        var vitalChanged = new List<GameEvent>();
        _eventBus.Subscribe("entity.vital.changed", e => vitalChanged.Add(e));

        var phase = new ResolveAutoAttacksPhase();
        phase.Execute(context);

        Assert.Contains(vitalChanged, e => e.SourceEntityId == boss.Id
            && (string)e.Data["vital"]! == "hp"
            && (string)e.Data["reason"]! == "combat.melee");
    }

    // Always rolls the maximum value for any Next(min, max) call, guaranteeing a critical
    // hit on the d20 (natural 20) and a maximal damage-dice roll -- deterministic damage
    // without depending on armor class or strength scaling.
    private class AlwaysHitRandom : Random
    {
        public override int Next(int minValue, int maxValue)
        {
            return maxValue - 1;
        }
    }
}
