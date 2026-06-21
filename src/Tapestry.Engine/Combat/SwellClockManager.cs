using Tapestry.Shared;

namespace Tapestry.Engine.Combat;

/// <summary>
/// The per-fight swell clock. Advanced every tick by SwellClockPulse. Reads all timing/content/
/// magnitude dials off the boss entity's properties, so a "tier" is data, not code. A swell boss is
/// any in-combat entity with a non-empty <c>swell_window</c> dial. Resolve (Task 5) is the one mutator.
/// </summary>
public class SwellClockManager
{
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly CombatManager _combat;
    private readonly WindowValidatorRegistry _validators;
    private readonly Random _random;
    private readonly Dictionary<Guid, SwellState> _fights = new();

    public SwellClockManager(
        World world,
        EventBus eventBus,
        CombatManager combat,
        WindowValidatorRegistry validators,
        Random? random = null)
    {
        _world = world;
        _eventBus = eventBus;
        _combat = combat;
        _validators = validators;
        _random = random ?? new Random();
    }

    public void AdvanceAll(long tick)
    {
        foreach (var boss in _combat.GetCombatants())
        {
            if (!IsSwellBoss(boss))
            {
                continue;
            }

            var state = EnsureFight(boss, tick);
            if (state == null)
            {
                continue;
            }

            Advance(boss, state, tick);
        }

        // Drop fights whose boss left combat or died.
        var stale = _fights.Keys
            .Where(id => _world.GetEntity(id) is not { } e || !_combat.IsInCombat(id) || e.Stats.Hp <= 0)
            .ToList();
        foreach (var id in stale)
        {
            _fights.Remove(id);
        }
    }

    private static bool IsSwellBoss(Entity e)
    {
        return !string.IsNullOrEmpty(e.GetProperty<string>("swell_window"));
    }

    private SwellState? EnsureFight(Entity boss, long tick)
    {
        if (_fights.TryGetValue(boss.Id, out var existing))
        {
            return existing;
        }

        var playerId = _combat.GetPrimaryTarget(boss.Id);
        if (playerId == null)
        {
            return null;
        }

        var state = new SwellState
        {
            Phase = SwellPhase.Baseline,
            PlayerId = playerId.Value,
            PhaseEndsAtTick = tick + NextBaselineGap(boss)
        };
        _fights[boss.Id] = state;
        return state;
    }

    private long NextBaselineGap(Entity boss)
    {
        var gap = boss.GetProperty<int>("swell_baseline_gap_ticks");
        var jitter = boss.GetProperty<int>("swell_jitter_ticks");
        if (jitter > 0)
        {
            gap += _random.Next(-jitter, jitter + 1);
        }
        return Math.Max(1, gap);
    }

    private void Advance(Entity boss, SwellState state, long tick)
    {
        switch (state.Phase)
        {
            case SwellPhase.Baseline:
                if (tick >= state.PhaseEndsAtTick)
                {
                    EnterTelegraph(boss, state, tick);
                }
                break;
            case SwellPhase.Telegraph:
                if (tick >= state.PhaseEndsAtTick)
                {
                    EnterWindow(boss, state, tick);
                }
                else if (state.Mode == "stretch" && tick >= state.DecelNextTick)
                {
                    EmitDecelBeat(boss, state, tick);
                }
                break;
            case SwellPhase.Window:
                // Commit-then-resolve and timeout-weather land in Task 5.
                break;
            case SwellPhase.Resolve:
                break;
        }
    }

    private void EnterTelegraph(Entity boss, SwellState state, long tick)
    {
        var pickLine2 = _random.Next(2) == 1;
        var prefix = pickLine2 ? "swell_line2" : "swell_line1";
        state.AttackLine = boss.GetProperty<string>($"{prefix}_id") ?? "";
        state.RequiredCounter = boss.GetProperty<string>($"{prefix}_counter") ?? "";
        state.Tell = boss.GetProperty<string>("swell_tell") ?? "full";
        state.Mode = boss.GetProperty<string>("swell_mode") ?? "stretch";
        state.CommittedVerb = null;
        state.Phase = SwellPhase.Telegraph;
        state.PhaseEndsAtTick = tick + Math.Max(1, boss.GetProperty<int>("swell_telegraph_ticks"));

        // Beat 0 carries the tell-valve wind-up (the identity read: full/shape/hidden).
        var text = ComposeTelegraph(boss, state, prefix);
        Publish("combat.swell.telegraph", boss, state, text);

        // Stretch: schedule the first decelerating cadence beat. Later beats wait longer (DecelGap),
        // so the wind-up decelerates across the telegraph budget - the playtest-winning feel, driven
        // by the per-tick clock. The auto-attack stays gated; the deceleration lives in these lines.
        state.DecelIdx = 0;
        state.DecelNextTick = tick + DecelGap(0);
    }

    private void EmitDecelBeat(Entity boss, SwellState state, long tick)
    {
        state.DecelIdx++;
        Publish("combat.swell.telegraph", boss, state, StretchCadenceLine(state.DecelIdx));
        state.DecelNextTick = tick + DecelGap(state.DecelIdx);
    }

    // Growing gaps so the wind-up decelerates (mirrors the spike's decelGap). Ticks, 100ms each.
    private static long DecelGap(int idx)
    {
        long[] gaps = { 2, 3, 4, 5, 6 };
        return idx < gaps.Length ? gaps[idx] : gaps[^1];
    }

    // Generic deceleration texture (the tempo feel, not boss identity - identity is the tell-valve
    // beat 0). Strict 7-bit ASCII, no em dashes.
    private static string StretchCadenceLine(int idx)
    {
        string[] lines =
        {
            "...the blow draws back, impossibly slow...",
            "...you read the arc of it...",
            "...time pulls like taffy...",
            "...the strike hangs at its apex...",
            "...and begins to fall..."
        };
        return idx - 1 < lines.Length ? lines[idx - 1] : "...still falling...";
    }

    private static string ComposeTelegraph(Entity boss, SwellState state, string prefix)
    {
        return state.Tell switch
        {
            "full" => boss.GetProperty<string>($"{prefix}_tell_full") ?? "The enemy winds up.",
            "shape" => boss.GetProperty<string>($"{prefix}_tell_shape") ?? "The enemy winds up.",
            _ => boss.GetProperty<string>("swell_tell_hidden") ?? "The enemy gathers something you cannot make out.",
        };
    }

    private void EnterWindow(Entity boss, SwellState state, long tick)
    {
        state.Phase = SwellPhase.Window;
        state.PhaseEndsAtTick = tick + Math.Max(1, boss.GetProperty<int>("swell_window_ticks"));
        Publish("combat.swell.window", boss, state, "An opening!");
    }

    private void Publish(string type, Entity boss, SwellState state, string text)
    {
        _eventBus.Publish(new GameEvent
        {
            Type = type,
            SourceEntityId = boss.Id,
            RoomId = boss.LocationRoomId,
            Data = new Dictionary<string, object?>
            {
                ["targetId"] = state.PlayerId.ToString(),
                ["text"] = text
            }
        });
    }
}
