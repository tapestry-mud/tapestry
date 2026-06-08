using Tapestry.Shared;

namespace Tapestry.Server.Tests.Fakes;

public class FakeConnection : IConnection
{
    public string Id { get; }
    public bool IsConnected { get; private set; } = true;
    public bool SupportsAnsi { get; init; } = false;
    public string? RemoteAddress { get; init; }
    public List<string> SentLines { get; } = new();
    public bool EchoSuppressed { get; private set; }

    public event Action<string>? OnInput;
    public event Action? OnDisconnected;
    public event Action<string>? OnDisconnectedWithReason;

    public FakeConnection() : this(Guid.NewGuid().ToString()) { }
    public FakeConnection(string id) { Id = id; }

    public void SendText(string text) { SentLines.Add(text); }
    public void SendLine(string text) { SentLines.Add(text + "\r\n"); }
    public void ClearScreen() { }
    public void SuppressEcho() { EchoSuppressed = true; }
    public void RestoreEcho() { EchoSuppressed = false; }
    public void Heartbeat() { }

    public void Disconnect(string reason)
    {
        IsConnected = false;
        OnDisconnectedWithReason?.Invoke(reason);
        OnDisconnected?.Invoke();
    }

    public void SimulateInput(string text) { OnInput?.Invoke(text); }
}
