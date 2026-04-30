using System.Text.Json;
using Tapestry.Shared;

namespace Tapestry.Networking;

public class WebSocketGmcpHandler : IGmcpHandler
{
    private readonly Action<string, object> _sendEnvelope;

    public bool GmcpActive => true;
    public Action<string, JsonElement>? OnGmcpMessage { get; set; }

    public WebSocketGmcpHandler(Action<string, object> sendEnvelope)
    {
        _sendEnvelope = sendEnvelope;
    }

    public void Send(string package, object payload)
    {
        _sendEnvelope(package, payload);
    }

    public bool SupportsPackage(string package) => true;

    public void HandleIncoming(string package, JsonElement data)
    {
        OnGmcpMessage?.Invoke(package, data);
    }
}
