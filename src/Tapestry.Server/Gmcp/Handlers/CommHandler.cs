using Tapestry.Contracts;
using Tapestry.Engine;
using Tapestry.Server.Gmcp;

namespace Tapestry.Server.Gmcp.Handlers;

public class CommHandler : IGmcpPackageHandler
{
    private readonly IGmcpConnectionManager _connectionManager;

    public string Name => "Comm";
    public IReadOnlyList<string> PackageNames { get; } = new[] { "Comm.Channel" };

    public CommHandler(IGmcpConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public void Configure() { }

    public void SendBurst(string connectionId, object entity) { }

    public void SendChannel(Guid entityId, string channel, string sender, string text)
    {
        _connectionManager.Send(entityId, "Comm.Channel", new { channel, sender, text });
    }
}
