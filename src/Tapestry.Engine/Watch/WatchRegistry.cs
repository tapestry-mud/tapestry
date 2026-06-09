using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Tapestry.Shared;

namespace Tapestry.Engine.Watch;

/// <summary>
/// Source of truth for who is watching whom. Keyed by the watched entity's stable
/// <c>entity.Id</c> so subscriptions survive the watched player's connection being
/// swapped on link-dead reconnect. A watcher (identified by <paramref name="watcherKey"/>)
/// watches at most one target at a time.
/// </summary>
public sealed class WatchRegistry
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, IConnection>> _byTarget = new();
    private readonly ConcurrentDictionary<string, Guid> _watcherTarget = new();

    public void Subscribe(Guid targetEntityId, string watcherKey, IConnection sink)
    {
        if (_watcherTarget.TryGetValue(watcherKey, out var prev) && prev != targetEntityId)
        {
            RemoveFrom(prev, watcherKey);
        }
        _watcherTarget[watcherKey] = targetEntityId;
        var sinks = _byTarget.GetOrAdd(targetEntityId, _ => new ConcurrentDictionary<string, IConnection>());
        sinks[watcherKey] = sink;
    }

    public bool Unsubscribe(string watcherKey)
    {
        if (!_watcherTarget.TryRemove(watcherKey, out var target)) { return false; }
        RemoveFrom(target, watcherKey);
        return true;
    }

    public IReadOnlyCollection<IConnection> GetSinks(Guid targetEntityId)
    {
        return _byTarget.TryGetValue(targetEntityId, out var sinks)
            ? sinks.Values.ToArray()
            : Array.Empty<IConnection>();
    }

    private void RemoveFrom(Guid target, string watcherKey)
    {
        if (_byTarget.TryGetValue(target, out var sinks))
        {
            sinks.TryRemove(watcherKey, out _);
            if (sinks.IsEmpty) { _byTarget.TryRemove(target, out _); }
        }
    }
}
