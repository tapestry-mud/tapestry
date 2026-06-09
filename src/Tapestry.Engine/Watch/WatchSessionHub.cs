using System.Collections.Concurrent;

namespace Tapestry.Engine.Watch;

/// <summary>
/// Tracks the live anonymous watch sessions so the periodic roster tick can push an updated roster
/// to every watcher (the roster is poll-driven because <see cref="SessionManager"/> fires no
/// add/remove events). Thread-safe: the game loop tick broadcasts while WS-accept threads register
/// and unregister. Keyed by the watch connection id.
/// </summary>
public sealed class WatchSessionHub
{
    private readonly ConcurrentDictionary<string, WatchSession> _sessions = new();

    public int Count => _sessions.Count;

    public void Register(WatchSession session) => _sessions[session.Id] = session;

    public void Unregister(string id) => _sessions.TryRemove(id, out _);

    /// <summary>Pushes <paramref name="roster"/> to every active watcher. The snapshot is computed
    /// once by the caller (the roster tick) and shared across all sinks.</summary>
    public void Broadcast(IReadOnlyList<WatchRosterEntry> roster)
    {
        foreach (var session in _sessions.Values)
        {
            session.SendRoster(roster);
        }
    }
}
