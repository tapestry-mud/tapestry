namespace Tapestry.Server.GameEntry;

/// <summary>The mutually-exclusive results of resolving game entry for an
/// already-authenticated identity.</summary>
public enum GameEntryResult
{
    /// <summary>Re-attached to a link-dead session (silent).</summary>
    Reconnected,
    /// <summary>Kicked a live session and attached to its entity (after y confirm).</summary>
    TookOver,
    /// <summary>A live session existed and the user declined takeover.</summary>
    Declined,
    /// <summary>No session for this character and the account is at its limit.</summary>
    OverLimit,
    /// <summary>Fresh spawn from disk.</summary>
    Spawned,
}
