using Tapestry.Shared;

namespace Tapestry.Engine.Color;

public class ColorRenderingConnection : IConnection
{
    private readonly IConnection _inner;
    private readonly ColorRenderer _renderer;

    public ColorRenderingConnection(IConnection inner, ColorRenderer renderer)
    {
        _inner = inner;
        _renderer = renderer;
    }

    public void SendLine(string text)
    {
        var rendered = _inner.SupportsAnsi
            ? _renderer.RenderAnsi(text)
            : _renderer.RenderPlain(text);
        _inner.SendLine(rendered);
    }

    public void SendText(string text)
    {
        var rendered = _inner.SupportsAnsi
            ? _renderer.RenderAnsi(text)
            : _renderer.RenderPlain(text);
        _inner.SendText(rendered);
    }

    public string Id => _inner.Id;
    public bool IsConnected => _inner.IsConnected;
    public bool SupportsAnsi => _inner.SupportsAnsi;
    public void ClearScreen() => _inner.ClearScreen();
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
