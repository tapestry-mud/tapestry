namespace Tapestry.Networking;

public interface IProtocolHandler
{
    byte OptionCode { get; }
    bool IsSessionLong { get; }
    Task NegotiateAsync(TelnetConnection connection, CancellationToken ct);
    void HandleRemoteDo(TelnetConnection connection);
    void HandleSubnegotiation(byte[] data);
}
