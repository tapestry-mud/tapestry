using Tapestry.Engine.Flow;
using Tapestry.Shared;

namespace Tapestry.Engine;

public class World
{
    private readonly Dictionary<string, Room> _rooms = new();
    private readonly Dictionary<Guid, Entity> _entities = new();
    private readonly PlayerCreator? _playerCreator;

    public World(PlayerCreator? playerCreator = null)
    {
        _playerCreator = playerCreator;
    }

    public void AddRoom(Room room)
    {
        _rooms[room.Id] = room;
    }

    public Room? GetRoom(string id)
    {
        return _rooms.GetValueOrDefault(id);
    }

    public void RemoveRoom(string id)
    {
        _rooms.Remove(id);
    }

    public IEnumerable<Room> AllRooms => _rooms.Values;

    public bool MoveEntity(Entity entity, Direction direction)
    {
        if (entity.LocationRoomId == null)
        {
            return false;
        }

        var currentRoom = GetRoom(entity.LocationRoomId);
        if (currentRoom == null)
        {
            return false;
        }

        var exit = currentRoom.GetExit(direction);
        if (exit == null)
        {
            return false;
        }

        var targetRoom = GetRoom(exit.TargetRoomId);
        if (targetRoom == null)
        {
            return false;
        }

        currentRoom.RemoveEntity(entity);
        targetRoom.AddEntity(entity);
        return true;
    }

    public bool MoveEntity(Entity entity, Direction direction, DoorService doorService, EventBus eventBus)
    {
        if (entity.LocationRoomId == null) { return false; }

        var currentRoom = GetRoom(entity.LocationRoomId);
        if (currentRoom == null) { return false; }

        var exit = currentRoom.GetExit(direction);
        if (exit == null) { return false; }

        // Door check: closed doors block movement
        if (exit.Door != null && exit.Door.IsClosed)
        {
            eventBus.Publish(new GameEvent
            {
                Type = "door.blocked",
                SourceEntityId = entity.Id,
                RoomId = currentRoom.Id,
                Data = new Dictionary<string, object?>
                {
                    ["roomId"] = currentRoom.Id,
                    ["direction"] = direction.ToShortString(),
                    ["actorId"] = entity.Id.ToString(),
                    ["doorName"] = exit.Door.Name
                }
            });
            return false;
        }

        var targetRoom = GetRoom(exit.TargetRoomId);
        if (targetRoom == null) { return false; }

        currentRoom.RemoveEntity(entity);
        targetRoom.AddEntity(entity);
        return true;
    }

    public void TrackEntity(Entity entity)
    {
        _entities[entity.Id] = entity;
    }

    public void UntrackEntity(Entity entity)
    {
        _entities.Remove(entity.Id);
    }

    public Entity? GetEntity(Guid id)
    {
        if (_entities.TryGetValue(id, out var entity))
        {
            return entity;
        }

        // Fallback: search rooms
        foreach (var room in _rooms.Values)
        {
            var found = room.Entities.FirstOrDefault(e => e.Id == id);
            if (found != null)
            {
                _entities[found.Id] = found;
                return found;
            }
        }

        // Fallback: pending players in PlayerCreator (mid-creation, not yet tracked)
        return _playerCreator?.GetEntity(id);
    }

    public IEnumerable<Entity> GetAllTrackedEntities()
    {
        return _entities.Values;
    }

    public IEnumerable<Entity> GetEntitiesByTag(string tag)
    {
        return _rooms.Values
            .SelectMany(r => r.Entities)
            .Where(e => e.HasTag(tag));
    }

    public IEnumerable<Entity> GetEntitiesInRoom(string roomId)
    {
        var room = GetRoom(roomId);
        return room?.Entities ?? Enumerable.Empty<Entity>();
    }
}
