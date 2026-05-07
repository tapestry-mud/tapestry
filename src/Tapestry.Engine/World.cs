using System.Collections.Immutable;
using Tapestry.Engine.Flow;
using Tapestry.Shared;

namespace Tapestry.Engine;

public class World : ITagObserver
{
    private readonly Dictionary<string, Room> _rooms = new();
    private readonly Dictionary<Guid, Entity> _entities = new();
    private readonly PlayerCreator? _playerCreator;

    private Dictionary<string, HashSet<Entity>> _readIndex = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, HashSet<Entity>> _writeIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _dirtyTags = new(StringComparer.OrdinalIgnoreCase);

    public int LastSwapDirtyCount { get; private set; }
    public int LastSwapTagCount { get; private set; }

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
        if (targetRoom == null)
        {
            return false;
        }

        currentRoom.RemoveEntity(entity);
        targetRoom.AddEntity(entity);
        return true;
    }

    public void TrackEntity(Entity entity)
    {
        _entities[entity.Id] = entity;
        entity.RegisterTagObserver(this);
        foreach (var tag in entity.Tags)
        {
            AddToWriteIndex(entity, tag);
        }
    }

    public void UntrackEntity(Entity entity)
    {
        _entities.Remove(entity.Id);
        entity.UnregisterTagObserver(this);
        foreach (var tag in entity.Tags)
        {
            RemoveFromWriteIndex(entity, tag);
        }
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

    public IReadOnlySet<Entity> GetEntitiesByTag(string tag)
    {
        if (_readIndex.TryGetValue(tag, out var set))
        {
            return set;
        }
        return ImmutableHashSet<Entity>.Empty;
    }

    public IEnumerable<Entity> GetEntitiesInRoom(string roomId)
    {
        var room = GetRoom(roomId);
        return room?.Entities ?? Enumerable.Empty<Entity>();
    }

    public void SwapTagBuffers()
    {
        LastSwapDirtyCount = _dirtyTags.Count;
        _readIndex = _writeIndex;
        _writeIndex = new Dictionary<string, HashSet<Entity>>(_readIndex, StringComparer.OrdinalIgnoreCase);
        _dirtyTags.Clear();
        LastSwapTagCount = _readIndex.Count;
    }

    void ITagObserver.OnTagAdded(Entity entity, string tag)
    {
        AddToWriteIndex(entity, tag);
    }

    void ITagObserver.OnTagRemoved(Entity entity, string tag)
    {
        RemoveFromWriteIndex(entity, tag);
    }

    private void AddToWriteIndex(Entity entity, string tag)
    {
        if (!_dirtyTags.Contains(tag))
        {
            _writeIndex[tag] = _readIndex.TryGetValue(tag, out var existing)
                ? new HashSet<Entity>(existing)
                : new HashSet<Entity>();
            _dirtyTags.Add(tag);
        }
        else if (!_writeIndex.ContainsKey(tag))
        {
            // Tag is dirty (mutated this tick) but its set was pruned -- start fresh.
            _writeIndex[tag] = new HashSet<Entity>();
        }
        _writeIndex[tag].Add(entity);
    }

    private void RemoveFromWriteIndex(Entity entity, string tag)
    {
        if (!_dirtyTags.Contains(tag))
        {
            _writeIndex[tag] = _readIndex.TryGetValue(tag, out var existing)
                ? new HashSet<Entity>(existing)
                : new HashSet<Entity>();
            _dirtyTags.Add(tag);
        }
        _writeIndex[tag].Remove(entity);
        if (_writeIndex[tag].Count == 0)
        {
            _writeIndex.Remove(tag);
            // Do NOT remove from _dirtyTags -- SwapTagBuffers clears it.
            // If AddToWriteIndex fires for this tag again before the next swap,
            // it must not re-clone from _readIndex (which still has pre-mutation state).
        }
    }
}
