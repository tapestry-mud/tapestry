using Tapestry.Engine.Login;

namespace Tapestry.Engine.Watch;

/// <summary>
/// The MVP watch roster: every online, playing player. Sourced from <see cref="SessionManager"/>,
/// whose <c>Add</c>/<c>Remove</c> fire no events — so the roster is re-pushed on a periodic tick
/// (see <see cref="WatchSessionHub"/>), not on change. The live session enumerable is snapshotted
/// with <c>ToList()</c> first to avoid the concurrent-modification hazard on the game loop. The
/// future <c>crawler</c>-tag filter narrows this selector with zero rework.
/// </summary>
public sealed class WatchRosterSource : IWatchRosterSource
{
    private readonly SessionManager _sessions;

    public WatchRosterSource(SessionManager sessions)
    {
        _sessions = sessions;
    }

    public IReadOnlyList<WatchRosterEntry> Snapshot()
    {
        return _sessions.AllSessions
            .Where(s => s.Phase == LoginPhase.Playing)
            .Select(s => new WatchRosterEntry(
                s.PlayerEntity.Id.ToString(),
                s.PlayerEntity.Name,
                s.PlayerEntity.LocationRoomId ?? ""))
            .ToList();
    }
}
