using Tapestry.Engine.Sustenance;
using Tapestry.Shared;

namespace Tapestry.Engine.Consumables;

public class ConsumableService
{
    private readonly World _world;
    private readonly EventBus _eventBus;

    public ConsumableService(World world, EventBus eventBus)
    {
        _world = world;
        _eventBus = eventBus;
    }

    private static int ReadInt(Entity item, string key)
    {
        var raw = item.GetProperty<object>(key);
        if (raw == null) { return 0; }
        return Convert.ToInt32(raw);
    }

    private static bool ReadBool(Entity item, string key, bool defaultValue = false)
    {
        var raw = item.GetProperty<object>(key);
        if (raw == null) { return defaultValue; }
        if (raw is bool b) { return b; }
        return Convert.ToBoolean(raw);
    }

    public ConsumableResult Consume(Guid entityId, Guid itemId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return new ConsumableResult(false, ConsumeReason.ItemNotFound); }

        var item = entity.Contents.FirstOrDefault(i => i.Id == itemId);
        if (item == null) { return new ConsumableResult(false, ConsumeReason.ItemNotFound); }

        var hasCharges = item.HasProperty(ConsumableProperties.Charges);
        var chargesValue = hasCharges ? ReadInt(item, ConsumableProperties.Charges) : 0;
        if (hasCharges && chargesValue <= 0)
        {
            return new ConsumableResult(false, ConsumeReason.NoCharges);
        }

        var consumeMethod = item.GetProperty<string?>(ConsumableProperties.ConsumeMethod);

        var consumingEvent = new GameEvent
        {
            Type = "item.consuming",
            SourceEntityId = entityId,
            TargetEntityId = itemId,
            Data = new Dictionary<string, object?>
            {
                ["entityId"] = entityId.ToString(),
                ["itemId"] = itemId.ToString(),
                ["consumeMethod"] = consumeMethod
            }
        };
        _eventBus.Publish(consumingEvent);
        if (consumingEvent.Cancelled)
        {
            return new ConsumableResult(false, ConsumeReason.Cancelled);
        }

        var sustenanceValue = ReadInt(item, ConsumableProperties.SustenanceValue);
        var effectId = item.GetProperty<string?>(ConsumableProperties.EffectId);
        var effectDuration = ReadInt(item, ConsumableProperties.EffectDuration);
        var effectData = item.GetProperty<Dictionary<string, object>?>(ConsumableProperties.EffectData);
        var itemName = item.Name;

        var destroyItem = false;
        if (hasCharges)
        {
            var newCharges = chargesValue - 1;
            item.SetProperty(ConsumableProperties.Charges, newCharges);

            if (newCharges <= 0)
            {
                var destroyOnEmpty = !item.HasProperty(ConsumableProperties.DestroyOnEmpty)
                    || ReadBool(item, ConsumableProperties.DestroyOnEmpty, defaultValue: true);
                if (destroyOnEmpty) { destroyItem = true; }
            }
        }
        else
        {
            destroyItem = true;
        }

        if (sustenanceValue > 0)
        {
            var current = entity.TryGetProperty<int>(SustenanceProperties.Sustenance, out var sustenanceVal)
                ? sustenanceVal
                : 100;
            entity.SetProperty(SustenanceProperties.Sustenance, Math.Min(100, current + sustenanceValue));
        }

        // Publish while item still exists so event handlers can read its tags and properties.
        _eventBus.Publish(new GameEvent
        {
            Type = "item.consumed",
            SourceEntityId = entityId,
            Data = new Dictionary<string, object?>
            {
                ["entityId"] = entityId.ToString(),
                ["itemId"] = itemId.ToString(),
                ["itemName"] = itemName,
                ["consumeMethod"] = consumeMethod,
                ["effectId"] = effectId,
                ["effectDuration"] = effectDuration,
                ["effectData"] = effectData,
                ["sustenanceValue"] = sustenanceValue
            }
        });

        if (destroyItem)
        {
            entity.RemoveFromContents(item);
            _world.UntrackEntity(item);
        }

        return new ConsumableResult(true, ConsumeReason.Success,
            itemId.ToString(), itemName, consumeMethod, sustenanceValue,
            effectId, effectDuration, effectData);
    }
}
