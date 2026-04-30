// src/Tapestry.Engine/Inventory/InventoryManager.cs
using Tapestry.Engine.Consumables;
using Tapestry.Engine.Containers;
using Tapestry.Engine.Economy;
using Tapestry.Shared;

namespace Tapestry.Engine.Inventory;

public class InventoryManager
{
    private readonly EventBus _eventBus;
    private readonly World _world;
    private readonly CurrencyService _currency;

    public InventoryManager(EventBus eventBus, World world, CurrencyService currency)
    {
        _eventBus = eventBus;
        _world = world;
        _currency = currency;
    }

    public bool PickUp(Entity entity, Entity item, bool silent = false)
    {
        if (ReadBool(item, ContainerProperties.Fixed)) { return false; }

        // Weight check
        var maxWeight = entity.GetProperty<int>(InventoryProperties.MaxCarryWeight);
        if (maxWeight > 0)
        {
            var currentWeight = GetCarryWeight(entity);
            var itemWeight = item.GetProperty<int>(InventoryProperties.Weight);
            if (currentWeight + itemWeight > maxWeight)
            {
                return false;
            }
        }

        // Remove from room
        if (item.LocationRoomId != null)
        {
            var room = _world.GetRoom(item.LocationRoomId);
            room?.RemoveEntity(item);
        }

        entity.AddToContents(item);
        if (_currency.TryAutoConvert(entity, item))
        {
            return true;
        }

        if (!silent)
        {
            _eventBus.Publish(new GameEvent
            {
                Type = "entity.item.picked_up",
                SourceEntityId = entity.Id,
                TargetEntityId = item.Id,
                RoomId = entity.LocationRoomId,
                Data = { ["itemName"] = item.Name }
            });
        }

        return true;
    }

    public bool Drop(Entity entity, Entity item, bool silent = false)
    {
        if (!entity.Contents.Contains(item))
        {
            return false;
        }

        entity.RemoveFromContents(item);

        if (entity.LocationRoomId != null)
        {
            var room = _world.GetRoom(entity.LocationRoomId);
            room?.AddEntity(item);
        }

        if (!silent)
        {
            _eventBus.Publish(new GameEvent
            {
                Type = "entity.item.dropped",
                SourceEntityId = entity.Id,
                TargetEntityId = item.Id,
                RoomId = entity.LocationRoomId,
                Data = { ["itemName"] = item.Name }
            });
        }

        return true;
    }

    public bool Give(Entity from, Entity to, Entity item, bool silent = false)
    {
        if (!from.Contents.Contains(item))
        {
            return false;
        }

        from.RemoveFromContents(item);
        to.AddToContents(item);
        if (_currency.TryAutoConvert(to, item))
        {
            return true;
        }

        if (!silent)
        {
            _eventBus.Publish(new GameEvent
            {
                Type = "entity.item.given",
                SourceEntityId = from.Id,
                TargetEntityId = to.Id,
                RoomId = from.LocationRoomId,
                Data = { ["itemId"] = item.Id, ["itemName"] = item.Name }
            });
        }

        return true;
    }

    public (bool Success, string? FailReason) PutInContainer(Entity actor, Entity item, Entity container)
    {
        if (!container.HasTag("container"))
        {
            return (false, "not_container");
        }

        var isFixed = ReadBool(container, ContainerProperties.Fixed);
        var isPublic = ReadBool(container, ContainerProperties.Public);
        var isInActorInventory = actor.Contents.Contains(container);
        var isInRoom = container.Container == null
            && _world.GetRoom(actor.LocationRoomId ?? "")?.Entities.Contains(container) == true;

        if (!isInActorInventory && !isInRoom && !isPublic)
        {
            return (false, "not_accessible");
        }

        var capacity = ReadInt(container, ContainerProperties.ContainerCapacity);
        if (capacity > 0 && container.Contents.Count >= capacity)
        {
            return (false, "full");
        }

        var rawWeightLimit = container.GetProperty<object>(ContainerProperties.ContainerWeightLimit);
        if (rawWeightLimit != null)
        {
            var weightLimit = Convert.ToDouble(rawWeightLimit);
            var currentWeight = container.Contents.Sum(e => Convert.ToDouble(e.GetProperty<object>(InventoryProperties.Weight) ?? 0));
            var itemWeight = Convert.ToDouble(item.GetProperty<object>(InventoryProperties.Weight) ?? 0);
            if (currentWeight + itemWeight > weightLimit)
            {
                return (false, "too_heavy");
            }
        }

        var preEvent = new GameEvent
        {
            Type = "container.item_adding",
            SourceEntityId = actor.Id,
            TargetEntityId = container.Id,
            Data = new Dictionary<string, object?>
            {
                ["containerId"] = container.Id.ToString(),
                ["itemId"] = item.Id.ToString(),
                ["actorId"] = actor.Id.ToString()
            }
        };
        _eventBus.Publish(preEvent);
        if (preEvent.Cancelled) { return (false, "cancelled"); }

        actor.RemoveFromContents(item);
        container.AddToContents(item);

        _eventBus.Publish(new GameEvent
        {
            Type = "container.item_added",
            SourceEntityId = actor.Id,
            TargetEntityId = container.Id,
            Data = new Dictionary<string, object?>
            {
                ["containerId"] = container.Id.ToString(),
                ["itemId"] = item.Id.ToString(),
                ["actorId"] = actor.Id.ToString()
            }
        });

        return (true, null);
    }

    public (bool Success, string? FailReason) FillItem(Entity actor, Entity target, Entity source)
    {
        var rawMaxCharges = target.GetProperty<object>(ConsumableProperties.MaxCharges);
        if (rawMaxCharges == null)
        {
            return (false, "not_fillable");
        }
        var maxCharges = Convert.ToInt32(rawMaxCharges);

        var fillSource = source.GetProperty<string?>(ContainerProperties.FillSource);
        if (fillSource == null)
        {
            return (false, "no_fill_source");
        }

        var rawFillSupply = source.GetProperty<object>(ContainerProperties.FillSupply);
        var fillSupply = rawFillSupply != null ? (int?)Convert.ToInt32(rawFillSupply) : null;
        if (fillSupply.HasValue && fillSupply.Value <= 0)
        {
            return (false, "source_empty");
        }

        var existingFillType = target.GetProperty<string?>(ContainerProperties.FillType);
        var existingCharges = ReadInt(target, ConsumableProperties.Charges);
        if (existingFillType != null
            && existingFillType != fillSource
            && existingCharges > 0)
        {
            return (false, "mixed_liquids");
        }

        // Fill to max
        target.SetProperty(ConsumableProperties.Charges, maxCharges);
        target.SetProperty(ContainerProperties.FillType, fillSource);

        if (fillSupply.HasValue)
        {
            source.SetProperty(ContainerProperties.FillSupply, fillSupply.Value - 1);
        }

        _eventBus.Publish(new GameEvent
        {
            Type = "item.filled",
            SourceEntityId = actor.Id,
            Data = new Dictionary<string, object?>
            {
                ["sourceId"] = source.Id.ToString(),
                ["targetId"] = target.Id.ToString(),
                ["fillType"] = fillSource
            }
        });

        return (true, null);
    }

    public static int GetCarryWeight(Entity entity)
    {
        return entity.Contents.Sum(e => e.GetProperty<int>(InventoryProperties.Weight));
    }

    private static int ReadInt(Entity entity, string key)
    {
        var raw = entity.GetProperty<object>(key);
        if (raw == null) { return 0; }
        return Convert.ToInt32(raw);
    }

    private static bool ReadBool(Entity entity, string key, bool defaultValue = false)
    {
        var raw = entity.GetProperty<object>(key);
        if (raw == null) { return defaultValue; }
        if (raw is bool b) { return b; }
        return Convert.ToBoolean(raw);
    }
}
