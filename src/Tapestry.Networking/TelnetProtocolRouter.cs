namespace Tapestry.Networking;

public class TelnetProtocolRouter : IDisposable
{
    private readonly Dictionary<byte, IProtocolHandler> _handlers = new();

    public void Register(IProtocolHandler handler)
    {
        _handlers[handler.OptionCode] = handler;
    }

    public void HandleSubnegotiation(byte option, byte[] data)
    {
        if (_handlers.TryGetValue(option, out var handler))
        {
            handler.HandleSubnegotiation(data);
        }
    }

    public T? GetHandler<T>(byte option) where T : class, IProtocolHandler
    {
        if (_handlers.TryGetValue(option, out var handler))
        {
            return handler as T;
        }
        return null;
    }

    public void Dispose()
    {
        _handlers.Clear();
    }
}
