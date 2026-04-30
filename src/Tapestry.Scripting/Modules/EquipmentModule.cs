using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Scripting.Services;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class EquipmentModule : IJintApiModule
{
    private readonly EquipmentManager _equipmentManager;
    private readonly SlotRegistry _slotRegistry;
    private readonly World _world;
    private readonly ApiTransfer _transfer;
    private string _emptyText = "-nothing-";

    public EquipmentModule(EquipmentManager equipmentManager, SlotRegistry slotRegistry, World world, ApiTransfer transfer)
    {
        _equipmentManager = equipmentManager;
        _slotRegistry = slotRegistry;
        _world = world;
        _transfer = transfer;
    }

    public string Namespace => "equipment";

    public object Build(JintEngine engine)
    {
        return new
        {
            equip = new Func<string, string, string, object>((entityId, keywordOrId, slot) =>
            {
                if (!Guid.TryParse(entityId, out var eid))
                {
                    return new { success = false, displaced = (object?)null };
                }

                var entity = _world.GetEntity(eid);
                if (entity == null)
                {
                    return new { success = false, displaced = (object?)null };
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
                    return new { success = false, displaced = (object?)null };
                }

                var result = _equipmentManager.Equip(entity, item, slot);

                object? displacedObj = null;
                if (result.Displaced != null)
                {
                    displacedObj = new { id = result.Displaced.Id.ToString(), name = result.Displaced.Name };
                }

                return new { success = result.Success, displaced = displacedObj };
            }),

            unequip = new Func<string, string, bool>((entityId, slot) =>
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

                return _equipmentManager.Unequip(entity, slot);
            }),

            unequipByKeyword = new Func<string, string, object?>((entityId, keyword) =>
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

                foreach (var (slotKey, equipped) in entity.Equipment)
                {
                    if (KeywordMatcher.FindByKeyword(new[] { equipped }, keyword) != null)
                    {
                        var itemName = equipped.Name;
                        _equipmentManager.Unequip(entity, slotKey);
                        return new { slot = slotKey, itemName = itemName };
                    }
                }

                return null;
            }),

            unequipAll = new Func<string, object[]>(entityId =>
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

                var unequipped = new List<object>();
                foreach (var slotKey in entity.Equipment.Keys.ToList())
                {
                    var item = entity.GetEquipment(slotKey);
                    if (item == null)
                    {
                        continue;
                    }

                    var itemName = item.Name;
                    _equipmentManager.Unequip(entity, slotKey);
                    unequipped.Add(new { slot = slotKey, itemName = itemName });
                }

                return unequipped.ToArray();
            }),

            unequipAllSilent = new Func<string, object[]>(entityId =>
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

                var unequipped = new List<object>();
                foreach (var slotKey in entity.Equipment.Keys.ToList())
                {
                    var item = entity.GetEquipment(slotKey);
                    if (item == null)
                    {
                        continue;
                    }

                    var itemName = item.Name;
                    _equipmentManager.Unequip(entity, slotKey, silent: true);
                    unequipped.Add(new { slot = slotKey, itemName = itemName });
                }

                return unequipped.ToArray();
            }),

            getSlots = new Func<string, object[]>(entityId =>
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

                var result = new List<object>();
                foreach (var slotDef in _slotRegistry.AllSlots)
                {
                    if (slotDef.Max == 1)
                    {
                        var equipped = entity.GetEquipment(slotDef.Name);
                        result.Add(new
                        {
                            slot = slotDef.Name,
                            slotDisplay = slotDef.Display,
                            itemName = equipped?.Name,
                            itemId = equipped?.Id.ToString(),
                            empty = equipped == null,
                            rarityKey = equipped?.GetProperty<string>(ItemProperties.Rarity),
                            essenceKey = equipped?.GetProperty<string>(ItemProperties.Essence)
                        });
                    }
                    else
                    {
                        for (var i = 0; i < slotDef.Max; i++)
                        {
                            var slotKey = $"{slotDef.Name}:{i}";
                            var equipped = entity.GetEquipment(slotKey);
                            result.Add(new
                            {
                                slot = slotKey,
                                slotDisplay = slotDef.Display,
                                itemName = equipped?.Name,
                                itemId = equipped?.Id.ToString(),
                                empty = equipped == null,
                                rarityKey = equipped?.GetProperty<string>(ItemProperties.Rarity),
                                essenceKey = equipped?.GetProperty<string>(ItemProperties.Essence)
                            });
                        }
                    }
                }

                return result.ToArray();
            }),

            setEmptyText = new Action<string>(text => { _emptyText = text; }),

            getEmptyText = new Func<string>(() => _emptyText),

            registerSlot = new Action<JsValue>(def =>
            {
                var obj = (ObjectInstance)def;
                var name = obj.Get("name").ToString();
                var display = obj.Get("display").ToString();

                var maxVal = obj.Get("max");
                var max = maxVal.Type == Types.Number ? (int)(double)maxVal.ToObject()! : 1;

                _slotRegistry.Register(new SlotDefinition(name, display, max));
            }),

            transferAll = new Action<string, string>((from, to) =>
            {
                _transfer.TransferAllEquipment(from, to);
            }),

            transferAllSilent = new Action<string, string>((from, to) =>
            {
                _transfer.TransferAllEquipment(from, to, silent: true);
            })
        };
    }
}
