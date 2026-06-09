namespace Tapestry.Engine.Watch;

/// <summary>
/// The per-write broadcast-visibility gate (Slice C) — the first <c>ShouldBroadcast</c> producer
/// seam. A producer that knows a particular write is private (e.g. the <c>viewer</c> pack's tell
/// override) wraps the write in <see cref="Run"/>; the watch <see cref="TeeConnection"/> then skips
/// mirroring that write to spectators while the player still receives it normally. Engine-neutral:
/// the engine ships the seam, a pack decides the policy (a snoop-MUD never suppresses; a viewer-MUD
/// does — same seam, opposite policy).
///
/// Backed by an <see cref="System.Threading.AsyncLocal{T}"/> so suppression scopes to the (sync or
/// awaited) write and never leaks across the game loop to unrelated output.
/// </summary>
public static class WatchBroadcastScope
{
    private static readonly System.Threading.AsyncLocal<bool> _suppressed = new();

    /// <summary>True when the current write must NOT be mirrored to watchers.</summary>
    public static bool Suppressed => _suppressed.Value;

    /// <summary>Runs <paramref name="action"/> with broadcast suppressed, restoring the prior
    /// state afterward (re-entrant safe).</summary>
    public static void Run(Action action)
    {
        var prev = _suppressed.Value;
        _suppressed.Value = true;
        try
        {
            action();
        }
        finally
        {
            _suppressed.Value = prev;
        }
    }
}
