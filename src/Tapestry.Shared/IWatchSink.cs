namespace Tapestry.Shared;

/// <summary>
/// An anonymous watch-mode connection: a read-only spectator sink (Slice B of watch mode).
/// It IS an <see cref="IConnection"/> — so the watch tee delivers a watched player's rendered
/// output to it through <see cref="IConnection.SendText"/>/<see cref="IConnection.SendLine"/> —
/// and it can additionally emit the two watch control channels as their own typed frames: a
/// <c>roster</c> of watchable players and a <c>status</c> line. A watch sink has no player
/// entity, no room, and no command dispatch; it can never affect the world.
/// </summary>
public interface IWatchSink : IConnection
{
    /// <summary>Pushes a <c>roster</c> frame (the current list of watchable players).</summary>
    void SendRoster(object roster);

    /// <summary>Pushes a <c>status</c> frame (a short human-readable line, e.g. "Now watching X").</summary>
    void SendStatus(string message);
}
