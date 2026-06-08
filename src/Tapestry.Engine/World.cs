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

    /// <summary>
    /// Re-key a room: re-index the dictionary, retarget exits of rooms in the SAME area,
    /// relocate entities standing in the room, and report (without touching) every
    /// pack-room or out-of-area referencer. In-memory only — side-car and connection
    /// persistence belong to the authoring layer.
    /// </summary>
    public RekeyResult RekeyRoom(string oldId, string newId)
    {
        var room = GetRoom(oldId);
        if (room == null || GetRoom(newId) != null)
        {
            return RekeyResult.Failed;
        }

        // Re-index first, inside this method body, so no caller can ever observe a
        // dictionary key that disagrees with room.Id.
        _rooms.Remove(oldId);
        room.Id = newId;
        _rooms[newId] = room;

        // The renamed room's own self-loop exits (an exit targeting itself) must follow
        // the new id too — the global scan below skips this room.
        room.RetargetExits(oldId, newId);

        // Global scan: every other room's exits. Same-area authored rooms are fixed;
        // pack rooms and other-area rooms are reported as edges, untouched.
        var retargeted = new List<string>();
        var edges = new List<RoomRef>();
        foreach (var other in _rooms.Values)
        {
            if (ReferenceEquals(other, room) || !other.HasExitTo(oldId))
            {
                continue;
            }

            var isPackRoom = other.GetRawProperty(CommonProperties.SourcePack) != null;
            var sameArea = !isPackRoom
                && other.Area != null
                && string.Equals(other.Area, room.Area, StringComparison.OrdinalIgnoreCase);
            if (sameArea)
            {
                other.RetargetExits(oldId, newId);
                retargeted.Add(other.Id);
            }
            else
            {
                edges.Add(new RoomRef(other.Id, other.Name, isPackRoom));
            }
        }

        // Entities standing in the room (players, NPCs, floor items) follow the id.
        foreach (var entity in room.Entities)
        {
            entity.LocationRoomId = newId;
        }

        return new RekeyResult
        {
            Ok = true,
            RetargetedRoomIds = retargeted,
            EdgeReferences = edges
        };
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

    /// <summary>
    /// Untracks an entity together with everything it carries -- every item in its
    /// Equipment and Contents, recursively (nested containers, equipped containers) --
    /// then the entity itself. Use this for any despawn of an entity that may hold
    /// items (player logout, corpse/mob removal, script purge). The plain UntrackEntity
    /// removes only the single entity, orphaning carried items in the tag index (they
    /// stay pinned, leaking memberships and heap).
    /// </summary>
    public void UntrackEntityDeep(Entity entity)
    {
        foreach (var equipped in entity.Equipment.Values.ToList())
        {
            UntrackEntityDeep(equipped);
        }
        foreach (var item in entity.Contents.ToList())
        {
            UntrackEntityDeep(item);
        }
        UntrackEntity(entity);
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

    public IEnumerable<Entity> GetEntitiesByType(string type)
    {
        return _entities.Values.Where(e => string.Equals(e.Type, type, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// One-pass snapshot of world content size for diagnostics metrics. Computed on
    /// demand (metric scrape) on the export thread, not cached. Returns null if a
    /// concurrent structural mutation on the game-loop thread makes the read throw:
    /// telemetry must never throw into the engine, and callers report a gap (no
    /// measurement) rather than a misleading zero.
    /// </summary>
    public WorldCensus? SampleCensus()
    {
        try
        {
            var census = new WorldCensus();
            foreach (var entity in _entities.Values)
            {
                census.EntitiesByType.TryGetValue(entity.Type, out var count);
                census.EntitiesByType[entity.Type] = count + 1;

                var propertyCount = entity.PropertyCount;
                census.PropertiesTotal += propertyCount;
                if (propertyCount > census.MaxEntityProperties)
                {
                    census.MaxEntityProperties = propertyCount;
                }
            }

            census.TagCount = _readIndex.Count;
            var memberships = 0;
            foreach (var set in _readIndex.Values)
            {
                memberships += set.Count;
            }
            census.TagMemberships = memberships;

            return census;
        }
        catch (Exception)
        {
            // Concurrent structural mutation on the game-loop thread (entity add/remove,
            // tag-index swap) can make these Dictionary enumerations throw on the export
            // thread. Telemetry must never throw into the engine; report a gap (null ->
            // no measurement), never a fake 0 that would mask the growth we're watching.
            return null;
        }
    }

    public IEnumerable<Entity> GetEntitiesByTemplateId(string templateId) =>
        _entities.Values.Where(e =>
            string.Equals(
                e.GetProperty<string>(CommonProperties.TemplateId),
                templateId,
                StringComparison.OrdinalIgnoreCase));

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
        else if (!_writeIndex.ContainsKey(tag))
        {
            // Tag is dirty (mutated this tick) but its set was pruned to empty --
            // there is nothing to remove. (Symmetric with AddToWriteIndex.)
            return;
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
