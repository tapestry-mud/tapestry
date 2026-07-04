namespace Tapestry.Scripting;

/// <summary>
/// Replaces Jint's built-in TimeoutInterval so a script that blows the per-entry
/// wall-clock budget surfaces a named, self-explaining exception instead of a bare
/// TimeoutException. The bare form historically read as arbitrary downstream failures
/// (a module import dying mid-evaluation leaves exports missing, so later calls throw
/// bogus ReferenceErrors that hide the real cause). Reset per top-level engine entry,
/// same semantics as Jint's TimeConstraint; pattern mirrors MobInvocationBudget.
/// </summary>
public sealed class ScriptTimeoutConstraint : Jint.Constraint
{
    private readonly TimeSpan _budget;
    private readonly TimeProvider _time;
    private long _startedAt;

    public ScriptTimeoutConstraint(TimeSpan budget, TimeProvider? time = null)
    {
        _budget = budget;
        _time = time ?? TimeProvider.System;
        _startedAt = _time.GetTimestamp();
    }

    public override void Check()
    {
        var elapsed = _time.GetElapsedTime(_startedAt);
        if (elapsed > _budget)
        {
            throw new ScriptTimeoutException(_budget, elapsed);
        }
    }

    public override void Reset()
    {
        _startedAt = _time.GetTimestamp();
    }
}

/// <summary>
/// Thrown when a pack script exceeds the per-entry Jint execution budget. Derives from
/// TimeoutException so existing generic catch paths are behavior-identical; only the
/// type and message sharpen.
/// </summary>
public sealed class ScriptTimeoutException : TimeoutException
{
    public TimeSpan Budget { get; }
    public TimeSpan Elapsed { get; }

    public ScriptTimeoutException(TimeSpan budget, TimeSpan elapsed)
        : base($"Script exceeded the {FormatMs(budget)} Jint execution budget " +
               $"(elapsed {FormatMs(elapsed)}). Long-running work must be chunked " +
               "across engine ticks (e.g. tapestry.schedule) — a single script entry " +
               "cannot run longer than the budget.")
    {
        Budget = budget;
        Elapsed = elapsed;
    }

    private static string FormatMs(TimeSpan t) => $"{t.TotalMilliseconds:0}ms";
}
