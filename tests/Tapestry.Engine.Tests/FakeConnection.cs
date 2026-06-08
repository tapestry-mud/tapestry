using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

internal class FakeConnection : IConnection
{
    public string Id { get; }
    public bool IsConnected { get; private set; } = true;
    public bool SupportsAnsi { get; init; } = false;
    public string? RemoteAddress { get; init; }
    public List<string> SentLines { get; } = new();
    public bool EchoSuppressed { get; private set; }
    public int HeartbeatCount { get; private set; }

    /// <summary>When set, a half-open peer is simulated: the next heartbeat write fails
    /// and (like the real connection) routes to <see cref="Disconnect"/>.</summary>
    public bool FailHeartbeat { get; set; }

    // Backward compat alias
    public List<string> SentText => SentLines;

    public event Action<string>? OnInput;
    public event Action? OnDisconnected;
    public event Action<string>? OnDisconnectedWithReason;

    public FakeConnection() : this(Guid.NewGuid().ToString()) { }

    public FakeConnection(string id)
    {
        Id = id;
    }

    public void SendText(string text)
    {
        SentLines.Add(text);
    }

    public void SendLine(string text)
    {
        SentLines.Add(text + "\r\n");
    }

    public void ClearScreen()
    {
        if (SupportsAnsi)
        {
            SendText("\x1b[2J\x1b[H");
        }
    }

    public void SuppressEcho() { EchoSuppressed = true; }
    public void RestoreEcho() { EchoSuppressed = false; }

    public void Heartbeat()
    {
        HeartbeatCount++;
        if (FailHeartbeat)
        {
            // Mirror the real connection: a failed heartbeat write tears down the connection.
            Disconnect("heartbeat write error");
        }
    }

    public void Disconnect(string reason)
    {
        IsConnected = false;
        OnDisconnectedWithReason?.Invoke(reason);
        OnDisconnected?.Invoke();
    }

    public void SimulateInput(string text)
    {
        OnInput?.Invoke(text);
    }
}
