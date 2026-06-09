namespace Tapestry.Engine.Watch;

/// <summary>
/// Supplies the current set of watchable players. Implementations snapshot the live session map
/// so callers never enumerate it concurrently with the game loop.
/// </summary>
public interface IWatchRosterSource
{
    IReadOnlyList<WatchRosterEntry> Snapshot();
}
