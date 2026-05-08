using Tapestry.Contracts;

namespace Tapestry.Engine.Tests.Gmcp;

internal class FakeGmcpConnectionManager : IGmcpConnectionManager
{
    public List<(string ConnectionId, string Package, object Payload)> Sent { get; } = new();

    public void RegisterHandler(string connectionId, Tapestry.Shared.IGmcpHandler handler) { }
    public void UnregisterHandler(string connectionId) { }

    public void Send(string connectionId, string package, object payload)
    {
        Sent.Add((connectionId, package, payload));
    }

    public void Send(Guid entityId, string package, object payload)
    {
        Sent.Add((entityId.ToString(), package, payload));
    }

    public bool SupportsPackage(Guid entityId, string package) => true;
    public virtual IEnumerable<string> GetActiveConnectionIds() => Enumerable.Empty<string>();
}
