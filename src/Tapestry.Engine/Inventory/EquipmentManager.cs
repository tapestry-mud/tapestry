// src/Tapestry.Engine/Inventory/EquipmentManager.cs
using Tapestry.Engine.Stats;
using Tapestry.Shared;

namespace Tapestry.Engine.Inventory;

public class EquipResult
{
    public static readonly EquipResult Failed = new(false, null);

    public bool Success { get; }
    public Entity? Displaced { get; }

    public EquipResult(bool success, Entity? displaced)
    {
        Success = success;
        Displaced = displaced;
    }
}

public class EquipmentManager
{
    private readonly SlotRegistry _slots;
    private readonly EventBus _eventBus;

    public EquipmentManager(SlotRegistry slots, EventBus eventBus)
    {
        _slots = slots;
        _eventBus = eventBus;
    }

    public EquipResult Equip(Entity entity, Entity item, string slotName)
    {
        // Validate slot exists
        var slotDef = _slots.GetSlot(slotName);
        if (slotDef == null)
        {
            return EquipResult.Failed;
        }

        // Validate item is in inventory
        if (!entity.Contents.Contains(item))
        {
            return EquipResult.Failed;
        }

        // Auto-swap if slot is full
        Entity? displaced = null;
        var slotsUsed = CountEquippedInSlot(entity, slotName);
        if (slotsUsed >= slotDef.Max)
        {
            // Unequip the first occupied sub-slot
            var swapKey = slotDef.Max == 1 ? slotName : $"{slotName}:0";
            displaced = entity.GetEquipment(swapKey);
            if (displaced == null)
            {
                return EquipResult.Failed;
            }
            Unequip(entity, swapKey);
        }

        // Find the actual slot key (for multi-slots: finger:0, finger:1)
        var slotKey = slotName;
        if (slotDef.Max > 1)
        {
            for (var i = 0; i < slotDef.Max; i++)
            {
                var candidateKey = $"{slotName}:{i}";
                if (entity.GetEquipment(candidateKey) == null)
                {
                    slotKey = candidateKey;
                    break;
                }
            }
        }

        entity.SetEquipment(slotKey, item);
        entity.RemoveFromContents(item);

        // Apply stat modifiers
        var modifiers = item.GetProperty<List<StatModifier>>(InventoryProperties.Modifiers);
        if (modifiers != null)
        {
            var source = $"equipment:{item.Id}";
            foreach (var mod in modifiers)
            {
                entity.Stats.AddModifier(new StatModifier(source, mod.Stat, mod.Value));
            }
        }

        _eventBus.Publish(new GameEvent
        {
            Type = "entity.equipped",
            SourceEntityId = entity.Id,
            RoomId = entity.LocationRoomId,
            Data =
            {
                ["itemId"] = item.Id,
                [InventoryProperties.Slot] = slotName
            }
        });

        return new EquipResult(true, displaced);
    }

    public bool Unequip(Entity entity, string slotKey, bool silent = false)
    {
        var item = entity.GetEquipment(slotKey);
        if (item == null)
        {
            return false;
        }

        entity.RemoveEquipment(slotKey);
        entity.AddToContents(item);

        // Remove stat modifiers
        var source = $"equipment:{item.Id}";
        entity.Stats.RemoveModifiersBySource(source);

        if (!silent)
        {
            // Extract base slot name for the event
            var slotName = slotKey.Contains(':') ? slotKey[..slotKey.IndexOf(':')] : slotKey;

            _eventBus.Publish(new GameEvent
            {
                Type = "entity.unequipped",
                SourceEntityId = entity.Id,
                RoomId = entity.LocationRoomId,
                Data =
                {
                    ["itemId"] = item.Id,
                    [InventoryProperties.Slot] = slotName
                }
            });
        }

        return true;
    }

    private int CountEquippedInSlot(Entity entity, string slotName)
    {
        var slotDef = _slots.GetSlot(slotName);
        if (slotDef == null)
        {
            return 0;
        }

        if (slotDef.Max == 1)
        {
            return entity.GetEquipment(slotName) != null ? 1 : 0;
        }

        var count = 0;
        for (var i = 0; i < slotDef.Max; i++)
        {
            if (entity.GetEquipment($"{slotName}:{i}") != null)
            {
                count++;
            }
        }

        return count;
    }
}
