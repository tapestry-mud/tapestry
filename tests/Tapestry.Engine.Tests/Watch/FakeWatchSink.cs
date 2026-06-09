using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Watch;

/// <summary>
/// Test double for <see cref="IWatchSink"/>: captures the three outbound watch channels
/// (<c>watch</c> via SendText/SendLine, <c>roster</c>, <c>status</c>) and lets a test drive the
/// inbound control protocol (<see cref="SimulateInput"/>) and a disconnect.
/// </summary>
internal sealed class FakeWatchSink : IWatchSink
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public bool IsConnected { get; private set; } = true;
    public bool SupportsAnsi => true;
    public string? RemoteAddress => null;

    public List<string> WatchOutputs { get; } = new();
    public List<object> Rosters { get; } = new();
    public List<string> Statuses { get; } = new();

    public event Action<string>? OnInput;
    public event Action? OnDisconnected;
    public event Action<string>? OnDisconnectedWithReason;

    public void SendText(string text) => WatchOutputs.Add(text);
    public void SendLine(string text) => WatchOutputs.Add(text + "\r\n");
    public void SendRoster(object roster) => Rosters.Add(roster);
    public void SendStatus(string message) => Statuses.Add(message);
    public void ClearScreen() { }
    public void Heartbeat() { }
    public void SuppressEcho() { }
    public void RestoreEcho() { }

    public void Disconnect(string reason)
    {
        IsConnected = false;
        OnDisconnectedWithReason?.Invoke(reason);
        OnDisconnected?.Invoke();
    }

    public void SimulateInput(string text) => OnInput?.Invoke(text);
}
