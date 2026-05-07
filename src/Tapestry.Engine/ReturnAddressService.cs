using Tapestry.Shared;

namespace Tapestry.Engine;

public class ReturnAddressService
{
    private const string PropertyKey = "return_room";
    private readonly EventBus _eventBus;

    public ReturnAddressService(EventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void SetReturn(Entity player, string roomId)
    {
        player.SetProperty(PropertyKey, roomId);
        _eventBus.Publish(new GameEvent
        {
            Type = "return.set",
            SourceEntityId = player.Id,
            Data = new Dictionary<string, object?>
            {
                ["entityId"] = player.Id.ToString(),
                ["roomId"] = roomId
            }
        });
    }

    public string? GetReturn(Entity player)
    {
        return player.GetProperty<string>(PropertyKey);
    }

    public void ClearReturn(Entity player)
    {
        player.SetProperty(PropertyKey, null);
    }

    public bool HasReturn(Entity player)
    {
        return player.TryGetProperty<string>(PropertyKey, out _);
    }
}
