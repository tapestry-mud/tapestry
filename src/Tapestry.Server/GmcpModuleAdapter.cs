using System.Text.Json;
using Tapestry.Contracts;
using Tapestry.Scripting.Modules;

namespace Tapestry.Server;

public class GmcpModuleAdapter : IGmcpModuleAdapter
{
    private readonly IGmcpConnectionManager _connectionManager;
    private readonly Dictionary<string, List<Action<Guid, object>>> _subscriptions = new(StringComparer.OrdinalIgnoreCase);

    public GmcpModuleAdapter(IGmcpConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public void Send(Guid entityId, string package, object payload)
    {
        _connectionManager.Send(entityId, package, payload);
    }

    public bool SupportsPackage(Guid entityId, string package)
    {
        return _connectionManager.SupportsPackage(entityId, package);
    }

    public void Subscribe(string package, Action<Guid, object> callback)
    {
        if (!_subscriptions.TryGetValue(package, out var list))
        {
            list = new List<Action<Guid, object>>();
            _subscriptions[package] = list;
        }
        list.Add(callback);
    }

    public void DispatchMessage(Guid entityId, string package, JsonElement data)
    {
        if (!_subscriptions.TryGetValue(package, out var list)) { return; }
        var obj = data.ValueKind == JsonValueKind.Object
            ? (object)data.Deserialize<Dictionary<string, object>>()!
            : data.GetRawText();
        foreach (var cb in list)
        {
            cb(entityId, obj);
        }
    }
}
