namespace Tapestry.Shared;

public interface IConnection
{
    string Id { get; }
    bool IsConnected { get; }
    bool SupportsAnsi { get; }
    void SendText(string text);
    void SendLine(string text);
    void ClearScreen();
    void Disconnect(string reason);
    void SuppressEcho();
    void RestoreEcho();
    event Action<string>? OnInput;
    event Action? OnDisconnected;
    event Action<string>? OnDisconnectedWithReason;
}
