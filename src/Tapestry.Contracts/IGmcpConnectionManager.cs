using Tapestry.Shared;

namespace Tapestry.Contracts;

public interface IGmcpConnectionManager
{
    void RegisterHandler(string connectionId, IGmcpHandler handler);
    void UnregisterHandler(string connectionId);
    void Send(string connectionId, string package, object payload);
    void Send(Guid entityId, string package, object payload);
    bool SupportsPackage(Guid entityId, string package);
    IEnumerable<string> GetActiveConnectionIds();
}
