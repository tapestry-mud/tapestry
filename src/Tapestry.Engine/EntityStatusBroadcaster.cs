using Tapestry.Engine.Persistence;
using Tapestry.Shared;

namespace Tapestry.Engine;

/// <summary>
/// The IPropertyObserver attached to tracked entities. When an observable property changes
/// (per PropertyRegistry.TryGetObservableTopic), it publishes entity.&lt;topic&gt;.changed so GMCP
/// (and any future consumer) refreshes. Non-observable keys never touch the bus.
/// </summary>
public class EntityStatusBroadcaster : IPropertyObserver
{
    private readonly PropertyRegistry _registry;
    private readonly EventBus _eventBus;

    public EntityStatusBroadcaster(PropertyRegistry registry, EventBus eventBus)
    {
        _registry = registry;
        _eventBus = eventBus;
    }

    public void OnPropertyChanged(Entity entity, string key, object? oldValue, object? newValue)
    {
        if (!_registry.TryGetObservableTopic(key, out var topic))
        {
            return;
        }

        _eventBus.Publish(new GameEvent
        {
            Type = $"entity.{topic}.changed",
            SourceEntityId = entity.Id,
            RoomId = entity.LocationRoomId,
            SourceEntityName = entity.Name,
            Data = new Dictionary<string, object?>
            {
                ["key"] = key,
                ["old"] = oldValue,
                ["new"] = newValue
            }
        });
    }
}
