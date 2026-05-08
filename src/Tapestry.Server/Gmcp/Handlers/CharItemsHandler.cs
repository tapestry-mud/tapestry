using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Server.Gmcp;

namespace Tapestry.Server.Gmcp.Handlers;

public class CharItemsHandler : IGmcpPackageHandler
{
    private readonly IGmcpConnectionManager _connectionManager;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly SlotRegistry _slotRegistry;
    private readonly RarityRegistry _rarityRegistry;
    private readonly EssenceRegistry _essenceRegistry;

    public string Name => "CharItems";
    public IReadOnlyList<string> PackageNames { get; } = new[] { "Char.Items", "Char.Equipment" };

    public CharItemsHandler(
        IGmcpConnectionManager connectionManager,
        SessionManager sessions,
        World world,
        EventBus eventBus,
        SlotRegistry slotRegistry,
        RarityRegistry rarityRegistry,
        EssenceRegistry essenceRegistry)
    {
        _connectionManager = connectionManager;
        _sessions = sessions;
        _world = world;
        _eventBus = eventBus;
        _slotRegistry = slotRegistry;
        _rarityRegistry = rarityRegistry;
        _essenceRegistry = essenceRegistry;
    }

    public void Configure()
    {
        _eventBus.Subscribe("entity.item.picked_up", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendItems(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("entity.item.dropped", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendItems(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("entity.item.given", evt =>
        {
            if (evt.SourceEntityId.HasValue) { SendItems(evt.SourceEntityId.Value); }
            if (evt.TargetEntityId.HasValue) { SendItems(evt.TargetEntityId.Value); }
        });

        _eventBus.Subscribe("entity.equipped", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendEquipment(evt.SourceEntityId.Value);
            SendItems(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("entity.unequipped", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendEquipment(evt.SourceEntityId.Value);
            SendItems(evt.SourceEntityId.Value);
        });
    }

    public void SendBurst(string connectionId, object entity)
    {
        var e = (Entity)entity;
        _connectionManager.Send(connectionId, "Char.Items", BuildItemsPayload(e));
        _connectionManager.Send(connectionId, "Char.Equipment", BuildEquipmentPayload(e));
    }

    private void SendItems(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }
        _connectionManager.Send(entityId, "Char.Items", BuildItemsPayload(entity));
    }

    private void SendEquipment(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }
        _connectionManager.Send(entityId, "Char.Equipment", BuildEquipmentPayload(entity));
    }

    private object BuildItemsPayload(Entity entity)
    {
        var items = entity.Contents
            .GroupBy(e => e.GetProperty<string?>("template_id") ?? e.Id.ToString())
            .Select(g =>
            {
                var first = g.First();
                var rarity = first.GetProperty<string?>(ItemProperties.Rarity);
                var essence = first.GetProperty<string?>(ItemProperties.Essence);
                return new
                {
                    id = first.Id.ToString(),
                    name = first.Name,
                    templateId = first.GetProperty<string?>("template_id"),
                    quantity = g.Count(),
                    rarity,
                    essence,
                    rarityTag = _rarityRegistry.FormatInline(rarity),
                    essenceTag = _essenceRegistry.Format(essence),
                };
            })
            .ToList();

        return new { items };
    }

    private object BuildEquipmentPayload(Entity entity)
    {
        var slots = new Dictionary<string, object?>();

        foreach (var slotDef in _slotRegistry.AllSlots)
        {
            if (slotDef.Max == 1)
            {
                slots[slotDef.Name] = BuildSlotPayload(entity.GetEquipment(slotDef.Name));
            }
            else
            {
                for (var i = 0; i < slotDef.Max; i++)
                {
                    var key = $"{slotDef.Name}:{i}";
                    slots[key] = BuildSlotPayload(entity.GetEquipment(key));
                }
            }
        }

        foreach (var (slotKey, equipped) in entity.Equipment)
        {
            if (!slots.ContainsKey(slotKey))
            {
                slots[slotKey] = BuildSlotPayload(equipped);
            }
        }

        return new { slots };
    }

    private object? BuildSlotPayload(Entity? equipped)
    {
        if (equipped == null) { return null; }
        var rarity = equipped.GetProperty<string?>(ItemProperties.Rarity);
        var essence = equipped.GetProperty<string?>(ItemProperties.Essence);
        return new
        {
            id = equipped.Id.ToString(),
            name = equipped.Name,
            rarity,
            essence,
            rarityTag = _rarityRegistry.FormatInline(rarity),
            essenceTag = _essenceRegistry.Format(essence),
        };
    }
}
