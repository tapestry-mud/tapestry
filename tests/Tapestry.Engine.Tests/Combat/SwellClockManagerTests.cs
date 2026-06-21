using Tapestry.Engine.Combat;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Combat;

public class SwellClockManagerTests
{
    private World _world = null!;
    private EventBus _eventBus = null!;
    private CombatManager _combat = null!;
    private WindowValidatorRegistry _validators = null!;
    private Room _room = null!;
    private List<GameEvent> _events = null!;

    private SwellClockManager Setup()
    {
        _world = new World();
        _eventBus = new EventBus();
        _combat = new CombatManager(_world, _eventBus);
        _validators = new WindowValidatorRegistry();
        _room = new Room("core:arena", "Arena", "A test arena.");
        _world.AddRoom(_room);
        _events = new List<GameEvent>();
        _eventBus.Subscribe("combat.swell.telegraph", e => _events.Add(e));
        _eventBus.Subscribe("combat.swell.window", e => _events.Add(e));
        _eventBus.Subscribe("combat.swell.resolve", e => _events.Add(e));
        return new SwellClockManager(_world, _eventBus, _combat, _validators);
    }

    private Entity CreatePlayer(int hp = 100)
    {
        var entity = new Entity("player", "Travis");
        entity.AddTag("player");
        entity.Stats.BaseMaxHp = hp;
        entity.Stats.Hp = hp;
        _room.AddEntity(entity);
        _world.TrackEntity(entity);
        return entity;
    }

    private Entity CreateBoss(int hp = 200)
    {
        var entity = new Entity("npc", "the swell-warden");
        entity.AddTag("npc");
        entity.Stats.BaseMaxHp = hp;
        entity.Stats.Hp = hp;
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
}
