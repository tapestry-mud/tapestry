using System;
using Tapestry.Shared;

namespace Tapestry.Engine.Watch;

/// <summary>
/// Output decorator that forwards every write to the player (<c>_inner</c>) and, when
/// <see cref="ShouldBroadcast"/> is true, mirrors it to every watcher subscribed to this
/// connection's owning entity. Inserted between ColorRenderingConnection and
/// WrappingConnection so watchers receive color-rendered, unwrapped ANSI. The owner is
/// resolved lazily (the entity is attached at login, after the chain is built), and the
/// registry keys on the stable <c>entity.Id</c>, so a reconnect (new connection, same
/// entity) keeps watchers attached.
/// </summary>
public sealed class TeeConnection : IConnection
{
    private readonly IConnection _inner;
    private readonly WatchRegistry _watch;
    private readonly Func<Guid?> _ownerResolver;

    /// <summary>The broadcast-visibility seam. Default true: an indiscriminate tee. A future
    /// producer sets this false for private writes (e.g. tells, a later slice).</summary>
    public bool ShouldBroadcast { get; set; } = true;

    public TeeConnection(IConnection inner, WatchRegistry watch, Func<Guid?> ownerResolver)
    {
        _inner = inner;
        _watch = watch;
        _ownerResolver = ownerResolver;
    }

    public void SendText(string text)
    {
        _inner.SendText(text);
        Broadcast(sink => sink.SendText(text));
    }

    public void SendLine(string text)
    {
        _inner.SendLine(text);
        Broadcast(sink => sink.SendLine(text));
    }

    private void Broadcast(Action<IConnection> deliver)
    {
        if (!ShouldBroadcast) { return; }
        var owner = _ownerResolver();
        if (owner is null) { return; }
        foreach (var sink in _watch.GetSinks(owner.Value))
        {
            if (sink.IsConnected) { deliver(sink); }
        }
    }

    public string Id => _inner.Id;
    public bool IsConnected => _inner.IsConnected;
    public bool SupportsAnsi => _inner.SupportsAnsi;
    public string? RemoteAddress => _inner.RemoteAddress;
    public void ClearScreen() => _inner.ClearScreen();
    public void Heartbeat() => _inner.Heartbeat();
    public void Disconnect(string reason) => _inner.Disconnect(reason);
    public void SuppressEcho() => _inner.SuppressEcho();
    public void RestoreEcho() => _inner.RestoreEcho();

    public event Action<string>? OnInput
    {
        add => _inner.OnInput += value;
        remove => _inner.OnInput -= value;
    }
    public event Action? OnDisconnected
    {
        add => _inner.OnDisconnected += value;
        remove => _inner.OnDisconnected -= value;
    }
    public event Action<string>? OnDisconnectedWithReason
    {
        add => _inner.OnDisconnectedWithReason += value;
        remove => _inner.OnDisconnectedWithReason -= value;
    }
}
