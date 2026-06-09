using Tapestry.Shared;

namespace Tapestry.Engine.Watch;

/// <summary>
/// One anonymous spectator's watch-mode session (Slice B). Owns the control protocol between an
/// <see cref="IWatchSink"/> transport and the engine <see cref="WatchRegistry"/>:
/// <list type="bullet">
/// <item><c>watch &lt;entityId&gt;</c> — subscribe the sink to that player's tee'd output, then send
/// a <c>status</c> line and an SGR reset (<c>ESC[0m</c>) so a mid-stream viewer starts from a clean
/// terminal state (design section 8).</item>
/// <item><c>unwatch</c> — drop the subscription.</item>
/// </list>
/// <c>next</c> is deliberately client-driven (the client holds the roster and just sends
/// <c>watch &lt;nextId&gt;</c>), so the server stays dumb. The sink is subscribed under its own stable
/// connection id as the watcher key and resolved live (<c>() =&gt; sink</c>), exactly like the snoop
/// command, so the registry's reconnect-safe contract is honored. On disconnect the subscription is
/// removed so the watcher set never leaks.
/// </summary>
public sealed class WatchSession
{
    private readonly IWatchSink _sink;
    private readonly WatchRegistry _watch;
    private readonly IWatchRosterSource _roster;

    public WatchSession(IWatchSink sink, WatchRegistry watch, IWatchRosterSource roster)
    {
        _sink = sink;
        _watch = watch;
        _roster = roster;

        _sink.OnInput += HandleInput;
        _sink.OnDisconnected += HandleDisconnected;
    }

    /// <summary>The watch connection id; also the watcher key in the <see cref="WatchRegistry"/>.</summary>
    public string Id => _sink.Id;

    /// <summary>The entityId currently watched, or null. Exposed for tests/diagnostics.</summary>
    public string? CurrentTarget { get; private set; }

    /// <summary>Pushes the current roster snapshot to this watcher (used on connect).</summary>
    public void PushRoster() => _sink.SendRoster(_roster.Snapshot());

    /// <summary>Pushes an already-computed roster snapshot — used by the periodic hub broadcast so
    /// the snapshot is computed once and shared across all watchers.</summary>
    public void SendRoster(IReadOnlyList<WatchRosterEntry> roster) => _sink.SendRoster(roster);

    private void HandleInput(string line)
    {
        line = (line ?? string.Empty).Trim();
        if (line.Length == 0) { return; }

        var space = line.IndexOf(' ');
        var verb = (space < 0 ? line : line[..space]).ToLowerInvariant();
        var arg = space < 0 ? string.Empty : line[(space + 1)..].Trim();

        switch (verb)
        {
            case "watch":
                Subscribe(arg);
                break;
            case "unwatch":
                Unsubscribe();
                break;
            default:
                // A watcher cannot affect the world; unknown control verbs are ignored.
                break;
        }
    }

    private void Subscribe(string targetIdStr)
    {
        if (!Guid.TryParse(targetIdStr, out var targetId))
        {
            _sink.SendStatus("Unknown stream.");
            return;
        }

        var normalized = targetId.ToString();
        var entry = _roster.Snapshot().FirstOrDefault(e => e.EntityId == normalized);
        if (entry is null)
        {
            _sink.SendStatus("That player is not available to watch.");
            return;
        }

        _watch.Subscribe(targetId, _sink.Id, () => _sink);
        CurrentTarget = entry.EntityId;
        _sink.SendStatus("Now watching " + entry.Name + ".");
        _sink.SendText("\x1b[0m"); // SGR reset: a mid-stream watcher starts from a clean state (design section 8).
    }

    private void Unsubscribe()
    {
        _watch.Unsubscribe(_sink.Id);
        CurrentTarget = null;
        _sink.SendStatus("Stopped watching.");
    }

    private void HandleDisconnected()
    {
        _watch.Unsubscribe(_sink.Id);
        CurrentTarget = null;
    }
}
