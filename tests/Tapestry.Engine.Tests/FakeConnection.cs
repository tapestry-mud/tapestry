using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

internal class FakeConnection : IConnection
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public bool IsConnected { get; private set; } = true;
    public bool SupportsAnsi { get; init; } = false;
    public List<string> SentText { get; } = new();
    public event Action<string>? OnInput;
    public event Action? OnDisconnected;
    public event Action<string>? OnDisconnectedWithReason;

    public void SendText(string text)
    {
        SentText.Add(text);
    }

    public void SendLine(string text)
    {
        SentText.Add(text + "\r\n");
    }

    public void ClearScreen()
    {
        if (SupportsAnsi)
        {
            SendText("\x1b[2J\x1b[H");
        }
    }

    public void SuppressEcho() { }
    public void RestoreEcho() { }

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
