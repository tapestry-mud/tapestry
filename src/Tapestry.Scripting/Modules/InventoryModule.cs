using Tapestry.Engine;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Engine.Stats;
using Tapestry.Scripting.Services;
using Tapestry.Shared;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class InventoryModule : IJintApiModule
{
    private readonly InventoryManager _inventoryManager;
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly ApiMessaging _messaging;
    private readonly ApiTransfer _transfer;
    private readonly SlotRegistry _slotRegistry;

    public InventoryModule(InventoryManager inventoryManager, World world, EventBus eventBus,
                          ApiMessaging messaging, ApiTransfer transfer, SlotRegistry slotRegistry)
    {
        _inventoryManager = inventoryManager;
        _world = world;
        _eventBus = eventBus;
        _messaging = messaging;
        _transfer = transfer;
        _slotRegistry = slotRegistry;
    }

    public string Namespace => "inventory";

    public object Build(JintEngine engine)
    {
        return new
        {
            pickUp = new Func<string, string, bool>((entityId, keyword) =>
            {
                if (!Guid.TryParse(entityId, out var eid))
                {
                    return false;
                }

                var entity = _world.GetEntity(eid);
                if (entity == null || entity.LocationRoomId == null)
                {
                    return false;
                }

                var room = _world.GetRoom(entity.LocationRoomId);
                if (room == null)
                {
                    return false;
                }

                var floorItems = room.Entities.Where(e => (e.HasTag("item") || e.HasTag("container")) && e.Container == null);
                var item = KeywordMatcher.FindByKeyword(floorItems, keyword);
                if (item == null)
                {
                    return false;
                }

                return _inventoryManager.PickUp(entity, item);
            }),

            drop = new Func<string, string, bool>((entityId, keyword) =>
            {
                if (!Guid.TryParse(entityId, out var eid))
                {
                    return false;
                }

                var entity = _world.GetEntity(eid);
                if (entity == null)
                {
                    return false;
                }

                var item = KeywordMatcher.FindByKeyword(entity.Contents, keyword);
                if (item == null)
                {
                    return false;
                }

                return _inventoryManager.Drop(entity, item);
            }),

            findByKeyword = new Func<string, string, object?>((entityId, keyword) =>
            {
                if (!Guid.TryParse(entityId, out var eid))
                {
                    return null;
                }

                var entity = _world.GetEntity(eid);
                if (entity == null)
                {
                    return null;
                }

                var item = KeywordMatcher.FindByKeyword(entity.Contents, keyword);
                if (item == null)
                {
                    return null;
                }

                return new { id = item.Id.ToString(), name = item.Name };
            }),

            findInRoom = new Func<string, string, object?>((entityId, keyword) =>
            {
                if (!Guid.TryParse(entityId, out var eid))
                {
                    return null;
                }

                var entity = _world.GetEntity(eid);
                if (entity == null || entity.LocationRoomId == null)
                {
                    return null;
                }

                var room = _world.GetRoom(entity.LocationRoomId);
                if (room == null)
                {
                    return null;
                }

                var floorItems = room.Entities.Where(e => (e.HasTag("item") || e.HasTag("container")) && e.Container == null);
                var item = KeywordMatcher.FindByKeyword(floorItems, keyword);
                if (item == null)
                {
                    return null;
                }

                return new { id = item.Id.ToString(), name = item.Name };
            }),

            getContents = new Func<string, object[]>(entityId =>
            {
                if (!Guid.TryParse(entityId, out var eid))
                {
                    return [];
                }

                var entity = _world.GetEntity(eid);
                if (entity == null)
                {
                    return [];
                }

                return entity.Contents
                    .Select(i => (object)new { id = i.Id.ToString(), name = i.Name })
                    .ToArray();
            }),

            getItemDetails = new Func<string, string, object?>((entityId, keywordOrId) =>
            {
                if (!Guid.TryParse(entityId, out var eid))
                {
                    return null;
                }

                var entity = _world.GetEntity(eid);
                if (entity == null)
                {
                    return null;
                }

                Entity? item = null;

                if (Guid.TryParse(keywordOrId, out var itemId))
                {
                    item = entity.Contents.FirstOrDefault(i => i.Id == itemId);
                }

                if (item == null)
                {
                    item = KeywordMatcher.FindByKeyword(entity.Contents, keywordOrId);
                }

                if (item == null)
                {
                    return null;
                }

                return new
                {
                    id = item.Id.ToString(),
                    name = item.Name,
                    slot = item.GetProperty<string>(InventoryProperties.Slot),
                    weight = item.GetProperty<int>(InventoryProperties.Weight),
                    rarity = item.GetProperty<string>(ItemProperties.Rarity)
                };
            }),

            examineItem = new Func<string, string, object?>((entityId, keyword) =>
            {
                if (!Guid.TryParse(entityId, out var eid))
                {
                    return null;
                }

                var entity = _world.GetEntity(eid);
                if (entity == null)
                {
                    return null;
                }

                Entity? item = null;
                string location = "inventory";

                item = KeywordMatcher.FindByKeyword(entity.Contents, keyword);

                if (item == null)
                {
                    foreach (var (slotKey, equipped) in entity.Equipment)
                    {
                        if (KeywordMatcher.FindByKeyword(new[] { equipped }, keyword) != null)
                        {
                            item = equipped;
                            location = "equipped";
                            break;
                        }
                    }
                }

                if (item == null && entity.LocationRoomId != null)
                {
                    var room = _world.GetRoom(entity.LocationRoomId);
                    if (room != null)
                    {
                        var floorItems = room.Entities.Where(e => (e.HasTag("item") || e.HasTag("container")) && e.Container == null);
                        item = KeywordMatcher.FindByKeyword(floorItems, keyword);
                        if (item != null)
                        {
                            location = "room";
                        }
                    }
                }

                if (item == null)
                {
                    return null;
                }

                var isContainer = item.HasTag("container");
                object[]? contents = null;
                if (isContainer)
                {
                    contents = item.Contents
                        .Select(i => (object)new { id = i.Id.ToString(), name = i.Name })
                        .ToArray();
                }

                var slotName = item.GetProperty<string>(InventoryProperties.Slot);
                string? slotDisplay = null;
                if (slotName != null)
                {
                    var slotDef = _slotRegistry.GetSlot(slotName);
                    slotDisplay = slotDef?.Display;
                }

                var modifiers = item.GetProperty<List<StatModifier>>(InventoryProperties.Modifiers);
                object[]? modArray = null;
                if (modifiers != null)
                {
                    modArray = modifiers
                        .Select(m => (object)new { stat = m.Stat.ToString(), value = m.Value })
                        .ToArray();
                }

                return new
                {
                    id = item.Id.ToString(),
                    name = item.Name,
                    slot = slotName,
                    slotDisplay = slotDisplay,
                    weight = item.GetProperty<int>(InventoryProperties.Weight),
                    rarity = item.GetProperty<string>(ItemProperties.Rarity),
                    modifiers = modArray,
                    location = location,
                    isContainer = isContainer,
                    contents = contents
                };
            }),

            findItemsInRoom = new Func<string, object[]>(entityId =>
            {
                if (!Guid.TryParse(entityId, out var eid))
                {
                    return [];
                }

                var entity = _world.GetEntity(eid);
                if (entity == null || entity.LocationRoomId == null)
                {
                    return [];
                }

                var room = _world.GetRoom(entity.LocationRoomId);
                if (room == null)
                {
                    return [];
                }

                return room.Entities
                    .Where(e => (e.HasTag("item") || e.HasTag("container")) && e.Container == null)
                    .Select(i => (object)new { id = i.Id.ToString(), name = i.Name })
                    .ToArray();
            }),

            getAll = new Func<string, string, object[]>((entityId, keyword) =>
            {
                if (!Guid.TryParse(entityId, out var eid))
                {
                    return [];
                }

                var entity = _world.GetEntity(eid);
                if (entity == null || entity.LocationRoomId == null)
                {
                    return [];
                }

                var room = _world.GetRoom(entity.LocationRoomId);
                if (room == null)
                {
                    return [];
                }

                var floorItems = room.Entities
                    .Where(e => (e.HasTag("item") || e.HasTag("container")) && e.Container == null && !e.HasTag("no_get"))
                    .ToList();
                var matches = KeywordMatcher.FindAllByKeyword(floorItems, keyword);

                var picked = new List<object>();
                foreach (var item in matches)
                {
                    if (_inventoryManager.PickUp(entity, item))
                    {
                        picked.Add(new { id = item.Id.ToString(), name = item.Name });
                    }
                }

                return picked.ToArray();
            }),

            dropAll = new Func<string, string, object[]>((entityId, keyword) =>
            {
                if (!Guid.TryParse(entityId, out var eid))
                {
                    return [];
                }

                var entity = _world.GetEntity(eid);
                if (entity == null)
                {
                    return [];
                }

                var matches = KeywordMatcher.FindAllByKeyword(entity.Contents, keyword);

                var dropped = new List<object>();
                foreach (var item in matches)
                {
                    if (_inventoryManager.Drop(entity, item))
                    {
                        dropped.Add(new { id = item.Id.ToString(), name = item.Name });
                    }
                }

                return dropped.ToArray();
            }),

            give = new Func<string, string, string, bool>((fromEntityId, toEntityId, keyword) =>
            {
                if (!Guid.TryParse(fromEntityId, out var fromId))
                {
                    return false;
                }

                if (!Guid.TryParse(toEntityId, out var toId))
                {
                    return false;
                }

                var from = _world.GetEntity(fromId);
                var to = _world.GetEntity(toId);
                if (from == null || to == null)
                {
                    return false;
                }

                var item = KeywordMatcher.FindByKeyword(from.Contents, keyword);
                if (item == null)
                {
                    return false;
                }

                return _inventoryManager.Give(from, to, item);
            }),

            findPlayerInRoom = new Func<string, string, object?>((entityId, name) =>
            {
                if (!Guid.TryParse(entityId, out var eid))
                {
                    return null;
                }

                var entity = _world.GetEntity(eid);
                if (entity == null || entity.LocationRoomId == null)
                {
                    return null;
                }

                var room = _world.GetRoom(entity.LocationRoomId);
                if (room == null)
                {
                    return null;
                }

                var player = room.Entities
                    .Where(e => e.HasTag("player") && e.Id != eid)
                    .FirstOrDefault(e => e.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));

                if (player == null)
                {
                    return null;
                }

                return new { id = player.Id.ToString(), name = player.Name };
            }),

            transferAll = new Action<string, string>((from, to) =>
            {
                _transfer.TransferAllInventory(from, to);
            }),

            transferAllSilent = new Action<string, string>((from, to) =>
            {
                _transfer.TransferAllInventory(from, to, silent: true);
            }),

            getContainerContents = new Func<string, string, object?>((entityId, containerKeyword) =>
            {
                if (!Guid.TryParse(entityId, out var eid))
                {
                    return null;
                }

                var entity = _world.GetEntity(eid);
                if (entity == null)
                {
                    return null;
                }

                var container = KeywordMatcher.FindByKeyword(
                    entity.Contents.Where(e => e.HasTag("container")), containerKeyword);

                if (container == null && entity.LocationRoomId != null)
                {
                    var room = _world.GetRoom(entity.LocationRoomId);
                    if (room != null)
                    {
                        var roomContainers = room.Entities.Where(e => e.HasTag("container") && e.Container == null);
                        container = KeywordMatcher.FindByKeyword(roomContainers, containerKeyword);
                    }
                }

                if (container == null)
                {
                    return null;
                }

                return new
                {
                    id = container.Id.ToString(),
                    name = container.Name,
                    items = container.Contents
                        .Select(i => (object)new { id = i.Id.ToString(), name = i.Name })
                        .ToArray()
                };
            }),

            getFromContainer = new Func<string, string, string, object?>((entityId, itemKeyword, containerKeyword) =>
            {
                if (!Guid.TryParse(entityId, out var eid))
                {
                    return null;
                }

                var entity = _world.GetEntity(eid);
                if (entity == null)
                {
                    return null;
                }

                var container = KeywordMatcher.FindByKeyword(
                    entity.Contents.Where(e => e.HasTag("container")), containerKeyword);

                if (container == null && entity.LocationRoomId != null)
                {
                    var room = _world.GetRoom(entity.LocationRoomId);
                    if (room != null)
                    {
                        var roomContainers = room.Entities.Where(e => e.HasTag("container") && e.Container == null);
                        container = KeywordMatcher.FindByKeyword(roomContainers, containerKeyword);
                    }
                }

                if (container == null)
                {
                    return null;
                }

                var accessEvent = new GameEvent
                {
                    Type = "container.access.check",
                    SourceEntityId = eid,
                    TargetEntityId = container.Id,
                    RoomId = entity.LocationRoomId,
                    Data = { ["action"] = "get" }
                };
                _eventBus.Publish(accessEvent);
                if (accessEvent.Cancelled)
                {
                    return new { denied = true, name = (string?)null, id = (string?)null };
                }

                var item = KeywordMatcher.FindByKeyword(container.Contents, itemKeyword);
                if (item == null)
                {
                    return null;
                }

                _inventoryManager.Give(container, entity, item, silent: true);

                return new { denied = false, name = item.Name, id = item.Id.ToString() };
            }),

            getAllFromContainer = new Func<string, string, object?>((entityId, containerKeyword) =>
            {
                if (!Guid.TryParse(entityId, out var eid))
                {
                    return null;
                }

                var entity = _world.GetEntity(eid);
                if (entity == null)
                {
                    return null;
                }

                var container = KeywordMatcher.FindByKeyword(
                    entity.Contents.Where(e => e.HasTag("container")), containerKeyword);

                if (container == null && entity.LocationRoomId != null)
                {
                    var room = _world.GetRoom(entity.LocationRoomId);
                    if (room != null)
                    {
                        var roomContainers = room.Entities.Where(e => e.HasTag("container") && e.Container == null);
                        container = KeywordMatcher.FindByKeyword(roomContainers, containerKeyword);
                    }
                }

                if (container == null)
                {
                    return null;
                }

                var accessEvent = new GameEvent
                {
                    Type = "container.access.check",
                    SourceEntityId = eid,
                    TargetEntityId = container.Id,
                    RoomId = entity.LocationRoomId,
                    Data = { ["action"] = "get" }
                };
                _eventBus.Publish(accessEvent);
                if (accessEvent.Cancelled)
                {
                    return new { denied = true, items = Array.Empty<object>() };
                }

                var items = container.Contents.ToList();
                var taken = new List<object>();
                foreach (var item in items)
                {
                    _inventoryManager.Give(container, entity, item, silent: true);
                    taken.Add(new { id = item.Id.ToString(), name = item.Name });
                }

                return new { denied = false, items = taken.ToArray() };
            }),

            putInContainer = new Func<string, string, string, object?>((entityId, itemKeyword, containerKeyword) =>
            {
                if (!Guid.TryParse(entityId, out var actorId)) { return null; }
                var actor = _world.GetEntity(actorId);
                if (actor == null) { return null; }

                var room = actor.LocationRoomId != null ? _world.GetRoom(actor.LocationRoomId) : null;

                var item = KeywordMatcher.FindByKeyword(actor.Contents.Where(e => !e.HasTag("container")), itemKeyword);
                if (item == null)
                {
                    var isContainer = KeywordMatcher.FindByKeyword(actor.Contents.Where(e => e.HasTag("container")), itemKeyword) != null;
                    var itemReason = isContainer ? "is_container" : "item_not_found";
                    return new { success = false, reason = itemReason, itemName = (string?)null, containerName = (string?)null };
                }

                // Accept GUID (donate.js) or keyword for container
                Entity? container = null;
                if (Guid.TryParse(containerKeyword, out var containerGuid))
                {
                    container = _world.GetEntity(containerGuid);
                }
                else
                {
                    container = KeywordMatcher.FindByKeyword(actor.Contents.Where(e => e.HasTag("container")), containerKeyword);
                    if (container == null && room != null)
                    {
                        container = KeywordMatcher.FindByKeyword(
                            room.Entities.Where(e => e.HasTag("container") && e.Container == null), containerKeyword);
                    }
                }
                if (container == null) { return new { success = false, reason = "container_not_found", itemName = (string?)null, containerName = (string?)null }; }

                var (success, failReason) = _inventoryManager.PutInContainer(actor, item, container);
                return new
                {
                    success,
                    reason = failReason ?? "success",
                    itemName = item.Name,
                    containerName = container.Name
                };
            }),

            putAllInContainer = new Func<string, string, object?>((entityId, containerKeyword) =>
            {
                if (!Guid.TryParse(entityId, out var eid))
                {
                    return null;
                }

                var entity = _world.GetEntity(eid);
                if (entity == null)
                {
                    return null;
                }

                var container = KeywordMatcher.FindByKeyword(
                    entity.Contents.Where(e => e.HasTag("container")), containerKeyword);

                if (container == null && entity.LocationRoomId != null)
                {
                    var room = _world.GetRoom(entity.LocationRoomId);
                    if (room != null)
                    {
                        var roomContainers = room.Entities.Where(e => e.HasTag("container") && e.Container == null);
                        container = KeywordMatcher.FindByKeyword(roomContainers, containerKeyword);
                    }
                }

                if (container == null)
                {
                    return null;
                }

                var accessEvent = new GameEvent
                {
                    Type = "container.access.check",
                    SourceEntityId = eid,
                    TargetEntityId = container.Id,
                    RoomId = entity.LocationRoomId,
                    Data = { ["action"] = "put" }
                };
                _eventBus.Publish(accessEvent);
                if (accessEvent.Cancelled)
                {
                    return new { denied = true, items = Array.Empty<object>() };
                }

                var items = entity.Contents.Where(e => !e.HasTag("container")).ToList();
                var placed = new List<object>();
                var stopReason = (string?)null;
                foreach (var item in items)
                {
                    var (success, failReason) = _inventoryManager.PutInContainer(entity, item, container);
                    if (success)
                    {
                        placed.Add(new { id = item.Id.ToString(), name = item.Name });
                    }
                    else if (failReason == "full" || failReason == "too_heavy")
                    {
                        stopReason = failReason;
                        break;
                    }
                }

                return new { denied = false, items = placed.ToArray(), containerName = container.Name, stopReason };
            }),

            fillItem = new Func<string, string, string, object?>((entityId, targetKeyword, sourceKeyword) =>
            {
                if (!Guid.TryParse(entityId, out var actorId)) { return null; }
                var actor = _world.GetEntity(actorId);
                if (actor == null) { return null; }
                var room = actor.LocationRoomId != null ? _world.GetRoom(actor.LocationRoomId) : null;

                // Target must be in inventory
                var target = KeywordMatcher.FindByKeyword(actor.Contents, targetKeyword);
                if (target == null) { return new { success = false, reason = "target_not_found", targetName = (string?)null, sourceName = (string?)null }; }

                // Source in room (fixture) or inventory
                Entity? source = null;
                if (room != null)
                {
                    source = KeywordMatcher.FindByKeyword(
                        room.Entities.Where(e => e.HasTag("fill_source") && e.Container == null), sourceKeyword);
                }
                source ??= KeywordMatcher.FindByKeyword(actor.Contents.Where(e => e.HasTag("fill_source")), sourceKeyword);
                if (source == null) { return new { success = false, reason = "source_not_found", targetName = (string?)null, sourceName = (string?)null }; }

                var (success, failReason) = _inventoryManager.FillItem(actor, target, source);
                return new
                {
                    success,
                    reason = failReason ?? "success",
                    targetName = target.Name,
                    sourceName = source.Name
                };
            })
        };
    }
}
