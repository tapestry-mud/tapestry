using Tapestry.Engine;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Mobs;
using Tapestry.Shared;

namespace Tapestry.Scripting.Services;

public class ApiWorld
{
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly SessionManager _sessions;
    private readonly MobAIManager _mobAIManager;
    private readonly AlignmentManager _alignmentManager;
    private readonly ApiMessaging _messaging;
    private readonly DoorService _doorService;

    public ApiWorld(World world, EventBus eventBus, SessionManager sessions, MobAIManager mobAIManager,
                    AlignmentManager alignmentManager, ApiMessaging messaging, DoorService doorService)
    {
        _world = world;
        _eventBus = eventBus;
        _sessions = sessions;
        _mobAIManager = mobAIManager;
        _alignmentManager = alignmentManager;
        _messaging = messaging;
        _doorService = doorService;
    }

    // --- Movement ---

    public bool MoveEntity(string entityIdStr, string directionStr)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return false;
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return false;
        }

        if (!DirectionExtensions.TryParse(directionStr, out var dir))
        {
            return false;
        }

        // Room alignment gate
        if (entity.LocationRoomId != null)
        {
            var currentRoom = _world.GetRoom(entity.LocationRoomId);
            if (currentRoom != null)
            {
                var exit = currentRoom.GetExit(dir);
                if (exit != null)
                {
                    var targetRoom = _world.GetRoom(exit.TargetRoomId);
                    if (targetRoom?.AlignmentRange != null && !entity.HasTag("admin"))
                    {
                        var alignment = _alignmentManager.Get(entityId);
                        if (!targetRoom.AlignmentRange.Allows(alignment))
                        {
                            var msg = targetRoom.AlignmentBlockMessage
                                ?? "A force turns you back at the threshold.\r\n";
                            _messaging.Send(entityId, msg);
                            _eventBus.Publish(new GameEvent
                            {
                                Type = "room.enter.blocked",
                                SourceEntityId = entity.Id,
                                RoomId = exit.TargetRoomId,
                                Data = new Dictionary<string, object?>
                                {
                                    ["entityId"] = entityIdStr,
                                    ["roomId"] = exit.TargetRoomId,
                                    ["reason"] = "alignment_restricted"
                                }
                            });
                            return false;
                        }
                    }
                }
            }
        }

        // Door check — closed doors block movement
        if (entity.LocationRoomId != null)
        {
            var currentRoom = _world.GetRoom(entity.LocationRoomId);
            if (currentRoom != null)
            {
                var exit = currentRoom.GetExit(dir);
                if (exit?.Door != null && exit.Door.IsClosed)
                {
                    _eventBus.Publish(new GameEvent
                    {
                        Type = "door.blocked",
                        SourceEntityId = entity.Id,
                        RoomId = currentRoom.Id,
                        Data = new Dictionary<string, object?>
                        {
                            ["roomId"] = currentRoom.Id,
                            ["direction"] = dir.ToShortString(),
                            ["actorId"] = entityIdStr,
                            ["doorName"] = exit.Door.Name
                        }
                    });
                    return false;
                }
            }
        }

        var oldRoomId = entity.LocationRoomId;
        var moved = _world.MoveEntity(entity, dir);

        if (moved)
        {
            _eventBus.Publish(new GameEvent
            {
                Type = "player.moved",
                SourceEntityId = entity.Id,
                RoomId = entity.LocationRoomId,
                Data = new Dictionary<string, object?>
                {
                    ["old_room_id"] = oldRoomId,
                    ["new_room_id"] = entity.LocationRoomId
                }
            });

            if (entity.HasTag("player") && entity.LocationRoomId != null)
            {
                _mobAIManager.OnPlayerEnteredRoom(entity.LocationRoomId, entityId);
            }

            if (entity.HasTag("npc") && entity.HasProperty("disposition") && entity.LocationRoomId != null)
            {
                _mobAIManager.OnMobEnteredRoom(entity, entity.LocationRoomId);
            }
        }

        return moved;
    }

    public bool TeleportEntity(string entityIdStr, string roomId)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return false;
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return false;
        }

        var oldRoomId = entity.LocationRoomId;
        var currentRoom = entity.LocationRoomId != null ? _world.GetRoom(entity.LocationRoomId) : null;
        currentRoom?.RemoveEntity(entity);

        var targetRoom = _world.GetRoom(roomId);
        if (targetRoom == null)
        {
            return false;
        }

        targetRoom.AddEntity(entity);

        _eventBus.Publish(new GameEvent
        {
            Type = "player.moved",
            SourceEntityId = entity.Id,
            RoomId = roomId,
            Data = new Dictionary<string, object?>
            {
                ["old_room_id"] = oldRoomId,
                ["new_room_id"] = roomId
            }
        });

        return true;
    }

    // --- Room queries ---

    public string[] GetRoomExits(string entityIdStr)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return [];
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null || entity.LocationRoomId == null)
        {
            return [];
        }

        var room = _world.GetRoom(entity.LocationRoomId);
        if (room == null)
        {
            return [];
        }

        return room.AvailableExits().Select(d => d.ToString().ToLowerInvariant()).ToArray();
    }

    public string? GetRoomName(string roomId)
    {
        var room = _world.GetRoom(roomId);
        return room?.Name;
    }

    public string? GetRoomDescription(string roomId)
    {
        var room = _world.GetRoom(roomId);
        return room?.Description;
    }

    public string? GetRoomArea(string roomId)
    {
        var room = _world.GetRoom(roomId);
        return room?.Area;
    }

    public string[] GetRoomTags(string roomId)
    {
        var room = _world.GetRoom(roomId);
        if (room == null)
        {
            return [];
        }

        return room.Tags.ToArray();
    }

    public bool SameArea(string roomA, string roomB)
    {
        return MobAIManager.GetAreaFromRoomId(roomA) == MobAIManager.GetAreaFromRoomId(roomB);
    }

    public string? GetExitTarget(string roomId, string direction)
    {
        if (!DirectionExtensions.TryParse(direction, out var dir))
        {
            return null;
        }

        var room = _world.GetRoom(roomId);
        if (room == null)
        {
            return null;
        }

        return room.GetExit(dir)?.TargetRoomId;
    }

    public object[] GetEntitiesInRoomByTag(string roomId, string tag)
    {
        var room = _world.GetRoom(roomId);
        if (room == null)
        {
            return [];
        }

        return room.Entities
            .Where(e => e.HasTag(tag))
            .Select(e => (object)new { id = e.Id.ToString(), name = e.Name, type = e.Type })
            .ToArray();
    }

    // --- Online players ---

    public object[] GetOnlinePlayers()
    {
        return _sessions.AllSessions
            .Select(s => (object)new { id = s.PlayerEntity.Id.ToString(), name = s.PlayerEntity.Name })
            .ToArray();
    }

    public object? FindPlayerByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var lower = name.ToLowerInvariant();
        foreach (var session in _sessions.AllSessions)
        {
            var playerName = session.PlayerEntity.Name;
            if (playerName.StartsWith(lower, StringComparison.OrdinalIgnoreCase))
            {
                return new { id = session.PlayerEntity.Id.ToString(), name = playerName };
            }
        }

        return null;
    }

    public void DisconnectPlayer(string entityIdStr)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return;
        }

        var session = _sessions.GetByEntityId(entityId);
        if (session != null)
        {
            session.Connection.Disconnect("Quit");
        }
    }

    // --- Entity queries ---

    public string? GetEntityName(string entityIdStr)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return null;
        }

        var entity = _world.GetEntity(entityId);
        return entity?.Name;
    }

    public string? GetEntityRoomId(string entityIdStr)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return null;
        }

        var entity = _world.GetEntity(entityId);
        return entity?.LocationRoomId;
    }

    // --- Entity management ---

    public string? CreateEntity(string type, string name)
    {
        var entity = new Entity(type, name);
        _world.TrackEntity(entity);
        return entity.Id.ToString();
    }

    public void AddEntityTag(string entityIdStr, string tag)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return;
        }

        var entity = _world.GetEntity(entityId);
        entity?.AddTag(tag);
    }

    public bool HasEntityTag(string entityIdStr, string tag)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return false;
        }

        var entity = _world.GetEntity(entityId);
        return entity?.HasTag(tag) ?? false;
    }

    public void SetEntityProperty(string entityIdStr, string key, object? value)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return;
        }

        var entity = _world.GetEntity(entityId);
        entity?.SetProperty(key, value);
    }

    public void PlaceEntityInRoom(string entityIdStr, string roomId)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return;
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return;
        }

        var room = _world.GetRoom(roomId);
        if (room == null)
        {
            return;
        }

        var currentRoom = entity.LocationRoomId != null ? _world.GetRoom(entity.LocationRoomId) : null;
        currentRoom?.RemoveEntity(entity);
        room.AddEntity(entity);
    }

    public void RemoveEntity(string entityIdStr)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return;
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return;
        }

        if (entity.LocationRoomId != null)
        {
            var room = _world.GetRoom(entity.LocationRoomId);
            room?.RemoveEntity(entity);
        }

        _world.UntrackEntity(entity);
    }

    public int PurgeEntities(string roomId, string filter)
    {
        var room = _world.GetRoom(roomId);
        if (room == null)
        {
            return 0;
        }

        var toRemove = new List<Entity>();
        foreach (var entity in room.Entities)
        {
            if (filter == "all" && entity.Type != "player")
            {
                toRemove.Add(entity);
            }
            else if (filter == "npc" && entity.Type == "npc")
            {
                toRemove.Add(entity);
            }
            else if (filter == "item" && entity.Type == "item")
            {
                toRemove.Add(entity);
            }
        }

        foreach (var entity in toRemove)
        {
            room.RemoveEntity(entity);
            _world.UntrackEntity(entity);
        }

        return toRemove.Count;
    }

    public object? GetEntityDetails(string entityIdStr)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return null;
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return null;
        }

        var stats = new
        {
            strength = entity.Stats.Strength,
            intelligence = entity.Stats.Intelligence,
            wisdom = entity.Stats.Wisdom,
            dexterity = entity.Stats.Dexterity,
            constitution = entity.Stats.Constitution,
            luck = entity.Stats.Luck,
            hp = entity.Stats.Hp,
            max_hp = entity.Stats.MaxHp,
            resource = entity.Stats.Resource,
            max_resource = entity.Stats.MaxResource,
            movement = entity.Stats.Movement,
            max_movement = entity.Stats.MaxMovement
        };

        var equipment = new Dictionary<string, object?>();
        foreach (var kv in entity.Equipment)
        {
            equipment[kv.Key] = new { name = kv.Value.Name, id = kv.Value.Id.ToString() };
        }

        var inventory = entity.Contents
            .Select(c => (object)new { name = c.Name, id = c.Id.ToString() })
            .ToArray();

        return new
        {
            id = entity.Id.ToString(),
            entityId = entity.Id.ToString(),
            name = entity.Name,
            type = entity.Type,
            roomId = entity.LocationRoomId,
            tags = entity.Tags.ToArray(),
            stats,
            equipment,
            inventory,
            properties = entity.GetAllProperties()
        };
    }
}
