using Tapestry.Engine;
using Tapestry.Engine.Inventory;

namespace Tapestry.Scripting.Services;

public class ApiTransfer
{
    private readonly World _world;
    private readonly InventoryManager _inventoryManager;
    private readonly EquipmentManager _equipmentManager;

    public ApiTransfer(World world, InventoryManager inventoryManager, EquipmentManager equipmentManager)
    {
        _world = world;
        _inventoryManager = inventoryManager;
        _equipmentManager = equipmentManager;
    }

    public void TransferAllInventory(string fromIdStr, string toIdStr, bool silent = false)
    {
        if (!Guid.TryParse(fromIdStr, out var fromId))
        {
            return;
        }

        if (!Guid.TryParse(toIdStr, out var toId))
        {
            return;
        }

        var from = _world.GetEntity(fromId);
        var to = _world.GetEntity(toId);
        if (from == null || to == null)
        {
            return;
        }

        foreach (var item in from.Contents.ToList())
        {
            _inventoryManager.Give(from, to, item, silent);
        }
    }

    public void TransferAllEquipment(string fromIdStr, string toIdStr, bool silent = false)
    {
        if (!Guid.TryParse(fromIdStr, out var fromId))
        {
            return;
        }

        if (!Guid.TryParse(toIdStr, out var toId))
        {
            return;
        }

        var from = _world.GetEntity(fromId);
        var to = _world.GetEntity(toId);
        if (from == null || to == null)
        {
            return;
        }

        foreach (var slot in from.Equipment.Keys.ToList())
        {
            var item = from.GetEquipment(slot);
            if (item == null)
            {
                continue;
            }

            _equipmentManager.Unequip(from, slot, silent);
            _inventoryManager.Give(from, to, item, silent);
        }
    }
}
