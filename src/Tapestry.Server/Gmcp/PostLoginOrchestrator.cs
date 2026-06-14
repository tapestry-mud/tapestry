using System.Diagnostics;
using Tapestry.Contracts;
using Tapestry.Engine;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Server.Gmcp;

public class PostLoginOrchestrator
{
    private static readonly Type[] DefaultBurstOrder =
    {
        typeof(DisplayHandler),
        typeof(CharStatusHandler),
        typeof(CharVitalsHandler),
        typeof(CharExperienceHandler),
        typeof(CharCommandsHandler),
        typeof(CharEffectsHandler),
        typeof(CharItemsHandler),
        typeof(RoomHandler),
        typeof(WorldHandler),
    };

    private readonly List<IGmcpPackageHandler> _orderedHandlers;

    public PostLoginOrchestrator(IEnumerable<IGmcpPackageHandler> handlers)
        : this(handlers, DefaultBurstOrder) { }

    internal PostLoginOrchestrator(IEnumerable<IGmcpPackageHandler> handlers, Type[] burstOrder)
    {
        var handlerList = handlers.ToList();
        _orderedHandlers = burstOrder
            .Select(t => handlerList.FirstOrDefault(h => h.GetType() == t))
            .Where(h => h != null)
            .Select(h => h!)
            .ToList();
    }

    public void SendPostLoginBurst(string connectionId, Entity entity)
    {
        foreach (var handler in _orderedHandlers)
        {
            // DIAGNOSTIC (telnet#90 follow-up): per-package span so a login trace breaks the
            // ScheduledActions burst (~124ms observed) down by package -- serialization + send
            // per handler. Pairs with the TelnetWrite span (raw=true) to split serialize vs
            // socket write. Remove or gate once the login-stall cause is settled.
            using var span = TapestryTracing.Source.StartActivity($"Gmcp.Burst.{handler.GetType().Name}");
            handler.SendBurst(connectionId, entity);
        }
    }
}
