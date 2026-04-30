using Tapestry.Shared;

namespace Tapestry.Engine.Rest;

public class RestService
{
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly GameLoop _gameLoop;

    public RestService(World world, EventBus eventBus, GameLoop gameLoop)
    {
        _world = world;
        _eventBus = eventBus;
        _gameLoop = gameLoop;
    }

    public string GetRestState(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        return entity?.GetProperty<string?>(RestProperties.RestState) ?? RestProperties.StateAwake;
    }

    public (bool Success, string? FailReason) SetRestState(Guid entityId, string newState, Guid? furnitureId = null)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return (false, "entity_not_found"); }

        var oldState = entity.GetProperty<string?>(RestProperties.RestState) ?? RestProperties.StateAwake;
        if (oldState == newState) { return (false, "already_in_state"); }

        var changeEvent = new GameEvent
        {
            Type = "entity.rest_state.changed",
            SourceEntityId = entityId,
            Data = new Dictionary<string, object?>
            {
                ["entityId"] = entityId.ToString(),
                ["oldState"] = oldState,
                ["newState"] = newState
            }
        };
        _eventBus.Publish(changeEvent);
        if (changeEvent.Cancelled) { return (false, "cancelled"); }

        entity.SetProperty(RestProperties.RestState, newState);

        if (newState == RestProperties.StateAwake)
        {
            entity.SetProperty(RestProperties.RestTarget, null);
        }
        else if (furnitureId.HasValue)
        {
            entity.SetProperty(RestProperties.RestTarget, furnitureId.Value.ToString());
        }

        if (newState == RestProperties.StateSleeping)
        {
            entity.SetProperty(RestProperties.SleepStartTick, _gameLoop.TickCount);
        }

        return (true, null);
    }
}
