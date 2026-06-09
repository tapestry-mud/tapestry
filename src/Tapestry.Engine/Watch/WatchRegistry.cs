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
///
/// Sinks are stored as <c>Func&lt;IConnection?&gt;</c> resolvers so a watcher's current
/// connection is resolved live on every broadcast — a watcher who reconnects (new
/// <see cref="IConnection"/> via <c>ReplaceConnection</c>) keeps receiving output, and a
/// logged-off watcher resolves to <c>null</c> (skipped) rather than holding a stale ref.
/// </summary>
public sealed class WatchRegistry
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, Func<IConnection?>>> _byTarget = new();
    private readonly ConcurrentDictionary<string, Guid> _watcherTarget = new();

    public void Subscribe(Guid targetEntityId, string watcherKey, Func<IConnection?> sinkResolver)
    {
        if (_watcherTarget.TryGetValue(watcherKey, out var prev) && prev != targetEntityId)
        {
            RemoveFrom(prev, watcherKey);
        }
        _watcherTarget[watcherKey] = targetEntityId;
        var resolvers = _byTarget.GetOrAdd(targetEntityId, _ => new ConcurrentDictionary<string, Func<IConnection?>>());
        resolvers[watcherKey] = sinkResolver;
    }

    public bool Unsubscribe(string watcherKey)
    {
        if (!_watcherTarget.TryRemove(watcherKey, out var target)) { return false; }
        RemoveFrom(target, watcherKey);
        return true;
    }

    public IReadOnlyCollection<IConnection> GetSinks(Guid targetEntityId)
    {
        if (!_byTarget.TryGetValue(targetEntityId, out var resolvers))
        {
            return Array.Empty<IConnection>();
        }
        return resolvers.Values
            .Select(r => r())
            .Where(c => c is not null)
            .ToArray()!;
    }

    /// <summary>
    /// Removes the target's entry entirely, and also removes each of its watcher keys
    /// from the reverse index so stale watcher-key lookups return false. Call this when
    /// the watched entity logs out / is reaped for good.
    /// </summary>
    public void RemoveTarget(Guid targetEntityId)
    {
        if (_byTarget.TryRemove(targetEntityId, out var resolvers))
        {
            foreach (var k in resolvers.Keys)
            {
                _watcherTarget.TryRemove(k, out _);
            }
        }
    }

    private void RemoveFrom(Guid target, string watcherKey)
    {
        if (_byTarget.TryGetValue(target, out var resolvers))
        {
            resolvers.TryRemove(watcherKey, out _);
            if (resolvers.IsEmpty) { _byTarget.TryRemove(target, out _); }
        }
    }
}
