using System.Collections.Concurrent;
using Tapestry.Contracts;
using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Server.Gmcp;

public class GmcpConnectionManager : IGmcpConnectionManager
{
    private readonly SessionManager _sessions;
    private readonly ConcurrentDictionary<string, IGmcpHandler> _handlers = new();

    public GmcpConnectionManager(SessionManager sessions)
    {
        _sessions = sessions;
    }

    public void RegisterHandler(string connectionId, IGmcpHandler handler)
    {
        _handlers[connectionId] = handler;
    }

    public void UnregisterHandler(string connectionId)
    {
        _handlers.TryRemove(connectionId, out _);
    }

    public void Send(string connectionId, string package, object payload)
    {
        if (!_handlers.TryGetValue(connectionId, out var handler)) { return; }
        if (!handler.GmcpActive) { return; }
        handler.Send(package, payload);
    }

    public void Send(Guid entityId, string package, object payload)
    {
        var session = _sessions.GetByEntityId(entityId);
        if (session == null) { return; }
        Send(session.Connection.Id, package, payload);
    }

    public bool SupportsPackage(Guid entityId, string package)
    {
        var session = _sessions.GetByEntityId(entityId);
        if (session == null) { return false; }
        if (!_handlers.TryGetValue(session.Connection.Id, out var handler)) { return false; }
        return handler.SupportsPackage(package);
    }

    public IEnumerable<string> GetActiveConnectionIds() => _handlers.Keys;
}
