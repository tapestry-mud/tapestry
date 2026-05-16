using Tapestry.Contracts;
using Tapestry.Engine;

namespace Tapestry.Server.Gmcp.Handlers;

public class NotificationHandler : IGmcpPackageHandler
{
    public string Name => "Notification";
    public IReadOnlyList<string> PackageNames { get; } = new[] { "Notification.Show" };

    private readonly IGmcpConnectionManager _connectionManager;

    public NotificationHandler(IGmcpConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public void Configure()
    {
    }

    public void SendBurst(string connectionId, object entity)
    {
    }

    public void DrainAndSend(Guid entityId, List<Notification> notifications)
    {
        foreach (var notification in notifications)
        {
            if (notification.GmcpPackage != null && notification.GmcpPayload != null)
            {
                _connectionManager.Send(entityId, notification.GmcpPackage, notification.GmcpPayload);
            }
        }
    }
}
