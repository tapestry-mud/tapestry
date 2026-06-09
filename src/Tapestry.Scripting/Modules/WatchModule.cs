using System;
using Tapestry.Engine;
using Tapestry.Engine.Watch;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

/// <summary>
/// Exposes the engine tee primitive to pack JS as <c>tapestry.watch</c>. The watcher's sink
/// is its own session connection, so no new transport is needed (the snoop case).
/// </summary>
public class WatchModule : IJintApiModule
{
    private readonly WatchRegistry _watch;
    private readonly SessionManager _sessions;

    public WatchModule(WatchRegistry watch, SessionManager sessions)
    {
        _watch = watch;
        _sessions = sessions;
    }

    public string Namespace => "watch";

    public object Build(JintEngine engine)
    {
        return new
        {
            start = new Func<string, string, bool>(Start),
            stop = new Func<string, bool>(Stop),
        };
    }

    public bool Start(string watcherEntityIdStr, string targetEntityIdStr)
    {
        if (!Guid.TryParse(watcherEntityIdStr, out var watcherId)) { return false; }
        if (!Guid.TryParse(targetEntityIdStr, out var targetId)) { return false; }

        // Self-subscription causes infinite recursion through the tee — reject it.
        if (watcherId == targetId) { return false; }

        var target = _sessions.GetByEntityId(targetId);
        if (target is null) { return false; }

        // Verify the watcher has a session at subscription time, but resolve live on
        // every broadcast so a reconnected watcher (new Connection, same entity id)
        // keeps receiving output without needing to re-subscribe.
        var watcher = _sessions.GetByEntityId(watcherId);
        if (watcher is null) { return false; }

        _watch.Subscribe(targetId, watcherId.ToString(), () => _sessions.GetByEntityId(watcherId)?.Connection);
        return true;
    }

    public bool Stop(string watcherEntityIdStr)
    {
        if (!Guid.TryParse(watcherEntityIdStr, out var watcherId)) { return false; }
        return _watch.Unsubscribe(watcherId.ToString());
    }
}
