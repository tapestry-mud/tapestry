using Tapestry.Shared;

namespace Tapestry.Engine.Text;

/// <summary>
/// <see cref="IConnection"/> decorator that word-wraps outbound text to a per-connection
/// visible width before handing it to the inner connection (which renders color). The
/// width is resolved lazily per write via the supplied provider, so it always reflects
/// the player's current screen_width preference / negotiated terminal width even though
/// the decorator is constructed at connect time (before login). A resolved width &lt;= 0
/// disables wrapping. Every other member delegates to the inner connection unchanged.
///
/// Sits OUTSIDE <see cref="Color.ColorRenderingConnection"/> so it wraps the markup
/// string (tags measured as zero-width) before it becomes ANSI -- see <see cref="MarkupWrapper"/>.
/// </summary>
public sealed class WrappingConnection : IConnection
{
    private readonly IConnection _inner;
    private readonly MarkupWrapper _wrapper;
    private readonly Func<int> _widthProvider;

    public WrappingConnection(IConnection inner, MarkupWrapper wrapper, Func<int> widthProvider)
    {
        _inner = inner;
        _wrapper = wrapper;
        _widthProvider = widthProvider;
    }

    public void SendText(string text) => _inner.SendText(_wrapper.Wrap(text, _widthProvider()));
    public void SendLine(string text) => _inner.SendLine(_wrapper.Wrap(text, _widthProvider()));

    public string Id => _inner.Id;
    public bool IsConnected => _inner.IsConnected;
    public bool SupportsAnsi => _inner.SupportsAnsi;
    public string? RemoteAddress => _inner.RemoteAddress;
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
