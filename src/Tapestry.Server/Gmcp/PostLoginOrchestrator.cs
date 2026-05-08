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
            handler.SendBurst(connectionId, entity);
        }
    }
}
