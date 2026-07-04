using Tapestry.Engine.Combat;
using Tapestry.Engine.Stats;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Combat;

public class SwellClockManagerTests
{
    private World _world = null!;
    private EventBus _eventBus = null!;
    private CombatManager _combat = null!;
    private WindowValidatorRegistry _validators = null!;
    private VitalsService _vitalsService = null!;
    private Room _room = null!;
    private List<GameEvent> _events = null!;

    private SwellClockManager Setup()
    {
        _world = new World();
        _eventBus = new EventBus();
        _combat = new CombatManager(_world, _eventBus, vitalsService: new VitalsService(_eventBus));
        _validators = new WindowValidatorRegistry();
        _vitalsService = new VitalsService(_eventBus);
        _room = new Room("core:arena", "Arena", "A test arena.");
        _world.AddRoom(_room);
        _events = new List<GameEvent>();
        _eventBus.Subscribe("combat.swell.telegraph", e => _events.Add(e));
        _eventBus.Subscribe("combat.swell.window", e => _events.Add(e));
        _eventBus.Subscribe("combat.swell.resolve", e => _events.Add(e));
        return new SwellClockManager(_world, _eventBus, _combat, _validators, vitalsService: _vitalsService);
    }

    private Entity CreatePlayer(int hp = 100)
    {
        var entity = new Entity("player", "Travis");
        entity.AddTag("player");
        entity.Stats.BaseMaxHp = hp;
        entity.Stats.SetVital(VitalKind.Hp, hp);
        _room.AddEntity(entity);
        _world.TrackEntity(entity);
        return entity;
    }

    private Entity CreateBoss(int hp = 200)
    {
        var entity = new Entity("npc", "the swell-warden");
        entity.AddTag("npc");
        entity.Stats.BaseMaxHp = hp;
        entity.Stats.SetVital(VitalKind.Hp, hp);
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
        entity.SetProperty("swell_line1_tell_shape", "The warden winds up a wide SWEEP.");
        entity.SetProperty("swell_line2_id", "crush");
        entity.SetProperty("swell_line2_counter", "brace");
        entity.SetProperty("swell_line2_tell_full", "The warden rears for a downward CRUSH -- brace for it!");
        entity.SetProperty("swell_line2_tell_shape", "The warden rears for a downward CRUSH.");
        entity.SetProperty("swell_tell_hidden", "The warden gathers something you cannot make out.");
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

    [Fact]
    public void Advance_EntersTelegraph_AfterBaselineGap_AndPublishesFullTell()
    {
        var clock = Setup();
        var player = CreatePlayer();
        var boss = CreateBoss();
        _combat.Engage(player, boss);

        // tick 0 registers the fight at Baseline with PhaseEndsAtTick = 0 + 2.
        clock.AdvanceAll(0);
        Assert.Empty(_events);
        // tick 1 still Baseline.
        clock.AdvanceAll(1);
        Assert.Empty(_events);
        // tick 2 crosses the gap -> Telegraph -> publishes beat 0 (the tell-valve wind-up).
        clock.AdvanceAll(2);

        var tele = _events.First(e => e.Type == "combat.swell.telegraph");
        Assert.Equal(player.Id.ToString(), tele.Data["targetId"]);
        var text = tele.Data["text"]!.ToString()!;
        // full tell names the attack AND the counter for one of the two lines.
        Assert.True(text.Contains("sidestep it!") || text.Contains("brace for it!"));
    }

    [Fact]
    public void Telegraph_StretchMode_EmitsDeceleratingWindUpBeats()
    {
        var clock = Setup();
        var player = CreatePlayer();
        var boss = CreateBoss();
        boss.SetProperty("swell_telegraph_ticks", 10); // long enough to fit several decel beats
        boss.SetProperty("swell_mode", "stretch");
        _combat.Engage(player, boss);

        // Drive every tick through the telegraph budget (the pulse calls AdvanceAll each tick).
        for (long t = 0; t <= 11; t++)
        {
            clock.AdvanceAll(t);
        }

        var telegraphs = _events.Where(e => e.Type == "combat.swell.telegraph").ToList();
        // beat 0 (the tell-valve identity line) plus at least one decelerating cadence beat.
        Assert.True(telegraphs.Count >= 2, $"expected a decelerating sequence, got {telegraphs.Count} beat(s)");
        var first = telegraphs[0].Data["text"]!.ToString()!;
        Assert.True(first.Contains("sidestep it!") || first.Contains("brace for it!")); // identity beat first
        Assert.Contains("...", telegraphs[1].Data["text"]!.ToString()!);                // a cadence beat follows
    }

    [Fact]
    public void Advance_EntersWindow_AfterTelegraph()
    {
        var clock = Setup();
        var player = CreatePlayer();
        var boss = CreateBoss();
        _combat.Engage(player, boss);

        clock.AdvanceAll(0); // Baseline, ends at 2
        clock.AdvanceAll(2); // -> Telegraph, ends at 4
        clock.AdvanceAll(4); // -> Window

        Assert.Contains(_events, e => e.Type == "combat.swell.window");
    }

    // --- Task 5 helpers ---

    private void RegisterDefaultValidator()
    {
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
    }

    private string RequiredCounterFromTelegraph()
    {
        var tele = _events.Last(e => e.Type == "combat.swell.telegraph");
        var text = tele.Data["text"]!.ToString()!;
        return text.Contains("SWEEP") ? "sidestep" : "brace";
    }

    private string WrongCounterFromTelegraph()
    {
        return RequiredCounterFromTelegraph() == "sidestep" ? "brace" : "sidestep";
    }

    [Fact]
    public void Countered_DamagesBoss_AndNegatesIncoming()
    {
        var clock = Setup();
        RegisterDefaultValidator();
        var player = CreatePlayer(hp: 100);
        var boss = CreateBoss(hp: 200);
        _combat.Engage(player, boss);

        clock.AdvanceAll(0);
        clock.AdvanceAll(2); // Telegraph (line + counter locked)
        clock.AdvanceAll(4); // Window open

        // Read which line was telegraphed from the published event, then commit its counter.
        var counter = RequiredCounterFromTelegraph();
        var result = clock.TryCommit(player.Id, counter);
        Assert.Equal(SwellCommitResult.Accepted, result);

        clock.AdvanceAll(5); // resolve

        Assert.Equal(100, player.Stats.Hp);          // incoming negated
        Assert.Equal(200 - 30, boss.Stats.Hp);       // 15% of 200 = 30
        Assert.Contains(_events, e => e.Type == "combat.swell.resolve");
    }

    [Fact]
    public void Whiffed_DamagesPlayer()
    {
        var clock = Setup();
        RegisterDefaultValidator();
        var player = CreatePlayer(hp: 100);
        var boss = CreateBoss(hp: 200);
        _combat.Engage(player, boss);

        clock.AdvanceAll(0);
        clock.AdvanceAll(2);
        clock.AdvanceAll(4);

        var wrong = WrongCounterFromTelegraph();
        clock.TryCommit(player.Id, wrong);
        clock.AdvanceAll(5);

        Assert.Equal(100 - 10, player.Stats.Hp);     // 10% of 100
        Assert.Equal(200, boss.Stats.Hp);
    }

    [Fact]
    public void Whiffed_PublishesEntityVitalChanged()
    {
        var clock = Setup();
        RegisterDefaultValidator();
        var player = CreatePlayer(hp: 100);
        var boss = CreateBoss(hp: 200);
        _combat.Engage(player, boss);

        var vitalChanged = new List<GameEvent>();
        _eventBus.Subscribe("entity.vital.changed", e => vitalChanged.Add(e));

        clock.AdvanceAll(0);
        clock.AdvanceAll(2);
        clock.AdvanceAll(4);

        var wrong = WrongCounterFromTelegraph();
        clock.TryCommit(player.Id, wrong);
        clock.AdvanceAll(5);

        Assert.Contains(vitalChanged, e => e.SourceEntityId == player.Id
            && (string)e.Data["vital"]! == "hp"
            && (string)e.Data["reason"]! == "combat.swell");
    }

    [Fact]
    public void Weathered_WhenNothingCommitted_DamagesPlayer()
    {
        var clock = Setup();
        RegisterDefaultValidator();
        var player = CreatePlayer(hp: 100);
        var boss = CreateBoss(hp: 200);
        _combat.Engage(player, boss);

        clock.AdvanceAll(0);
        clock.AdvanceAll(2);
        clock.AdvanceAll(4); // Window opens, ends at 8
        clock.AdvanceAll(8); // timeout -> weather -> resolve

        Assert.Equal(100 - 8, player.Stats.Hp);      // 8% of 100
    }

    [Fact]
    public void TryCommit_OffWindow_IsRejected()
    {
        var clock = Setup();
        RegisterDefaultValidator();
        var player = CreatePlayer();
        var boss = CreateBoss();
        _combat.Engage(player, boss);

        clock.AdvanceAll(0); // Baseline
        Assert.Equal(SwellCommitResult.NotInWindow, clock.TryCommit(player.Id, "sidestep"));
    }

    [Fact]
    public void Countered_Lethal_FiresVitalDepleted()
    {
        var clock = Setup();
        RegisterDefaultValidator();
        var player = CreatePlayer(hp: 100);
        var boss = CreateBoss(hp: 25); // 15% of MaxHp = 3; boss is near death so chunk is lethal
        boss.Stats.SetVital(VitalKind.Hp, 1); // already near death; the chunk finishes it
        _combat.Engage(player, boss);
        var depleted = new List<GameEvent>();
        _eventBus.Subscribe("entity.vital.depleted", e => depleted.Add(e));

        clock.AdvanceAll(0);
        clock.AdvanceAll(2);
        clock.AdvanceAll(4);
        clock.TryCommit(player.Id, RequiredCounterFromTelegraph());
        clock.AdvanceAll(5);

        Assert.Equal(0, boss.Stats.Hp);
        Assert.Contains(depleted, e => e.SourceEntityId == boss.Id);
    }

    [Fact]
    public void IsAutoAttackSuppressed_TrueDuringSwell_FalseAtBaseline()
    {
        var clock = Setup();
        RegisterDefaultValidator();
        var player = CreatePlayer();
        var boss = CreateBoss();
        _combat.Engage(player, boss);

        clock.AdvanceAll(0); // Baseline
        Assert.False(clock.IsAutoAttackSuppressed(player.Id, boss.Id));

        clock.AdvanceAll(2); // Telegraph
        Assert.True(clock.IsAutoAttackSuppressed(player.Id, boss.Id));
        Assert.True(clock.IsAutoAttackSuppressed(boss.Id, player.Id)); // either direction
    }

    [Fact]
    public void IsCombatSuppressed_BehavesLikeIsAutoAttackSuppressed()
    {
        var clock = Setup();
        RegisterDefaultValidator();
        var player = CreatePlayer();
        var boss = CreateBoss();
        _combat.Engage(player, boss);

        clock.AdvanceAll(0); // Baseline
        Assert.False(clock.IsCombatSuppressed(player.Id, boss.Id));

        clock.AdvanceAll(2); // Telegraph
        Assert.True(clock.IsCombatSuppressed(player.Id, boss.Id));
        Assert.True(clock.IsCombatSuppressed(boss.Id, player.Id));
    }

    [Fact]
    public void IsActorInActiveSwell_FalseAtBaseline_TrueDuringTelegraph()
    {
        var clock = Setup();
        RegisterDefaultValidator();
        var player = CreatePlayer();
        var boss = CreateBoss();
        _combat.Engage(player, boss);

        clock.AdvanceAll(0); // Baseline
        Assert.False(clock.IsActorInActiveSwell(player.Id));

        clock.AdvanceAll(2); // Telegraph
        Assert.True(clock.IsActorInActiveSwell(player.Id));
    }

    [Fact]
    public void IsKnownCounterVerb_MatchesLine1Counter()
    {
        var clock = Setup();
        var player = CreatePlayer();
        var boss = CreateBoss(); // swell_line1_counter = "sidestep", swell_line2_counter = "brace"
        _combat.Engage(player, boss);

        clock.AdvanceAll(0);
        clock.AdvanceAll(2); // Telegraph - picks either line1 or line2

        // Both counters should be recognised; non-counter verbs should not.
        var line1Match = clock.IsKnownCounterVerb(player.Id, "sidestep");
        var line2Match = clock.IsKnownCounterVerb(player.Id, "brace");
        // At least one of the two known counters matches (the one telegraphed is locked).
        // The OTHER one may or may not match depending on implementation; the important
        // thing is that a random verb does NOT match.
        Assert.True(line1Match || line2Match);
        Assert.False(clock.IsKnownCounterVerb(player.Id, "fireball"));
    }

    [Fact]
    public void IsKnownCounterVerb_FalseWhenNotInCombat()
    {
        var clock = Setup();
        // No combat, no boss target
        var player = CreatePlayer();
        Assert.False(clock.IsKnownCounterVerb(player.Id, "sidestep"));
    }
}
