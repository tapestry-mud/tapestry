namespace Tapestry.Shared;

public interface IConnection
{
    string Id { get; }
    bool IsConnected { get; }
    bool SupportsAnsi { get; }
    string? RemoteAddress { get; }
    void SendText(string text);
    void SendLine(string text);
    void ClearScreen();
    /// <summary>
    /// Sends a tiny liveness probe to the peer over the connection's normal write path.
    /// Its only purpose is to provoke a write so a half-open connection (peer vanished
    /// without a clean FIN/Close) surfaces as a write error and tears the connection down
    /// through the usual disconnect flow. A failed probe MUST route to <see cref="Disconnect"/>.
    /// Implementations whose transport already does framework-level liveness (e.g. WebSocket
    /// ping/pong) may make this a no-op.
    /// </summary>
    void Heartbeat();
    void Disconnect(string reason);
    void SuppressEcho();
    void RestoreEcho();
    event Action<string>? OnInput;
    event Action? OnDisconnected;
    event Action<string>? OnDisconnectedWithReason;
}
