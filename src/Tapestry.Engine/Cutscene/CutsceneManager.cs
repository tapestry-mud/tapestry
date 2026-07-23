namespace Tapestry.Engine.Cutscene;

/// <summary>
/// Layer 2 of the output-cadence-cutscenes spec (2026-07-22-output-cadence-cutscenes-design.md,
/// section 3): a scripting-facing cutscene player built on the Layer-1 prompt-hold gate
/// (PlayerSession.OpenPromptHold/ReleasePromptHold, shipped engine 0.1.52). Content is a beat
/// sequence (data, authored in packs); the engine owns pacing and the prompt lifecycle -- the
/// same "rules in the engine, content in packs" split SwellClockManager uses for combat.
///
/// Lifecycle: Play opens a hold (owner "cutscene") and emits beat 0 immediately -> AdvanceAll,
/// driven every tick by CutscenePulse, emits each further beat once its predecessor's
/// PauseAfterTicks has elapsed -> the final beat's emission releases the hold and fires
/// onComplete exactly once. skip (honored only when Skippable) flushes every remaining beat
/// immediately (zero delay) and takes the identical release+onComplete path -- never a
/// different, divergent completion state, only a faster one.
///
/// Never reimplements the Layer-1 gate: every hold open/release call goes through the existing
/// PlayerSession API, so disconnect/link-death auto-release (already wired for ALL hold owners)
/// covers a cutscene exactly like it covers a swell.
/// </summary>
public class CutsceneManager
{
    public const string HoldOwner = "cutscene";

    /// <summary>Default inter-beat pause when a beat's author omits pauseAfter: 20 ticks
    /// (2s at the engine's 100ms tick, the same cadence unit SwellClockManager's dials use).</summary>
    public const int DefaultPauseAfterTicks = 20;

    private readonly SessionManager _sessions;
    private readonly Dictionary<Guid, CutsceneInstance> _active = new();

    public CutsceneManager(SessionManager sessions)
    {
        _sessions = sessions;
    }

    public bool IsActive(Guid playerId)
    {
        return _active.ContainsKey(playerId);
    }

    /// <summary>
    /// Starts a cutscene for the given player. A no-op (onComplete still fires -- there is
    /// nothing to hold the prompt for) when the beat list is empty. A no-op entirely (onComplete
    /// does NOT fire) when the player has no live session -- nothing owns a completion in that
    /// state; the caller is expected to check online status first for a real player-facing flow.
    /// </summary>
    public void Play(Guid playerId, IReadOnlyList<CutsceneBeat> beats, bool skippable, long currentTick, Action? onComplete)
    {
        if (beats.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        var session = _sessions.GetByEntityId(playerId);
        if (session == null)
        {
            return;
        }

        // Defensive: a second Play() for a player already mid-cutscene drops the stale instance
        // without firing its onComplete. One opener per player in practice; this just prevents
        // two instances from fighting over the same hold if it ever happens.
        _active.Remove(playerId);

        var instance = new CutsceneInstance
        {
            PlayerId = playerId,
            Beats = beats,
            Skippable = skippable,
            OnComplete = onComplete
        };
        instance.InputHandler = input => HandleInput(playerId, input);

        _active[playerId] = instance;
        session.ActiveCutscene = instance;
        session.OpenPromptHold(HoldOwner);

        if (skippable)
        {
            session.Send("(type skip to skip)\r\n");
        }

        EmitBeat(session, instance, currentTick);
    }

    /// <summary>Ticked every engine tick by CutscenePulse. Emits the next beat for any instance
    /// whose cadence has elapsed.</summary>
    public void AdvanceAll(long tick)
    {
        if (_active.Count == 0)
        {
            return;
        }

        foreach (var instance in _active.Values.ToList())
        {
            if (instance.Completed)
            {
                continue;
            }

            var session = _sessions.GetByEntityId(instance.PlayerId);
            if (session == null || !ReferenceEquals(session.ActiveCutscene, instance))
            {
                // The session is gone, or a brand new PlayerSession now owns this entity id (a
                // fresh reconnect after a hard disconnect creates a new session object). Drop
                // the stale instance silently -- no onComplete, nothing legitimately owns it.
                _active.Remove(instance.PlayerId);
                continue;
            }

            if (tick >= instance.NextEmitTick)
            {
                EmitBeat(session, instance, tick);
            }
        }
    }

    private void EmitBeat(PlayerSession session, CutsceneInstance instance, long tick)
    {
        var beat = instance.Beats[instance.NextIndex];
        session.Send(beat.Text + "\r\n");
        instance.NextIndex++;

        if (instance.NextIndex >= instance.Beats.Count)
        {
            Complete(session, instance);
        }
        else
        {
            instance.NextEmitTick = tick + Math.Max(0, beat.PauseAfterTicks);
        }
    }

    private void HandleInput(Guid playerId, string input)
    {
        if (!_active.TryGetValue(playerId, out var instance) || instance.Completed)
        {
            return;
        }

        // Everything else is swallowed, including "skip" when the cutscene is not skippable --
        // treated exactly like any other line the player types during the hold.
        if (!instance.Skippable || !input.Trim().Equals("skip", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var session = _sessions.GetByEntityId(playerId);
        if (session == null || !ReferenceEquals(session.ActiveCutscene, instance))
        {
            return;
        }

        Skip(session, instance);
    }

    /// <summary>Flushes, never discards: every remaining beat's text still prints, with zero
    /// delay between them, then takes the identical completion path as natural playback.</summary>
    private void Skip(PlayerSession session, CutsceneInstance instance)
    {
        while (instance.NextIndex < instance.Beats.Count)
        {
            session.Send(instance.Beats[instance.NextIndex].Text + "\r\n");
            instance.NextIndex++;
        }

        Complete(session, instance);
    }

    private void Complete(PlayerSession session, CutsceneInstance instance)
    {
        instance.Completed = true;
        _active.Remove(instance.PlayerId);
        session.ActiveCutscene = null;
        session.ReleasePromptHold(HoldOwner);

        var callback = instance.OnComplete;
        instance.OnComplete = null;
        callback?.Invoke();
    }
}
