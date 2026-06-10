using Tapestry.Engine.Mobs;

namespace Tapestry.Scripting;

/// <summary>
/// Armable per-invocation wall-clock budget for mob behavior scripts, registered once
/// on the shared Jint engine. Disarmed (the default) it never throws, so every other
/// script on the engine pays only a timestamp comparison per constraint check. The
/// scripting layer arms it around each behavior invocation; a violation throws
/// MobBudgetExceededException, which Jint propagates to the C# caller (Jint 4.9.3
/// checks constraints inside bulk built-ins, so hot built-in loops are interruptible).
/// Single-threaded by design: the game loop owns the engine and the arming.
/// </summary>
public sealed class MobInvocationBudget : Jint.Constraint
{
    private readonly TimeProvider _time;
    private double _capMs; // <= 0 means disarmed
    private long _armedAt;

    public MobInvocationBudget(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Arms the budget; dispose to disarm (always disarm in a finally/using —
    /// a constraint left armed would bill the next unrelated script).</summary>
    public IDisposable Arm(double capMs)
    {
        _capMs = capMs;
        _armedAt = _time.GetTimestamp();
        return new Disarmer(this);
    }

    public override void Check()
    {
        if (_capMs <= 0)
        {
            return;
        }
        if (_time.GetElapsedTime(_armedAt).TotalMilliseconds > _capMs)
        {
            var cap = _capMs;
            _capMs = 0; // disarm before throwing so unwind/catch paths can't re-throw
            throw new MobBudgetExceededException(cap);
        }
    }

    public override void Reset()
    {
        // Jint resets constraints on each top-level entry. If a behavior's JS calls
        // back into C# which re-enters the engine, the clock restarts per segment;
        // each segment stays capped and MobAIManager's wall measurement remains the
        // strike authority, so this is bounded and accepted.
        if (_capMs > 0)
        {
            _armedAt = _time.GetTimestamp();
        }
    }

    private sealed class Disarmer : IDisposable
    {
        private readonly MobInvocationBudget _owner;

        public Disarmer(MobInvocationBudget owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            _owner._capMs = 0;
        }
    }
}
