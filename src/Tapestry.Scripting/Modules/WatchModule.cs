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

        var watcher = _sessions.GetByEntityId(watcherId);
        var target = _sessions.GetByEntityId(targetId);
        if (watcher is null || target is null) { return false; }

        _watch.Subscribe(targetId, watcherId.ToString(), watcher.Connection);
        return true;
    }

    public bool Stop(string watcherEntityIdStr)
    {
        if (!Guid.TryParse(watcherEntityIdStr, out var watcherId)) { return false; }
        return _watch.Unsubscribe(watcherId.ToString());
    }
}
