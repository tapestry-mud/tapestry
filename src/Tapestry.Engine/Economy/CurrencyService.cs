using Tapestry.Shared;

namespace Tapestry.Engine.Economy;

public class CurrencyService
{
    private readonly World _world;
    private readonly EventBus _eventBus;

    public CurrencyService(World world, EventBus eventBus)
    {
        _world = world;
        _eventBus = eventBus;
    }

    public bool TryAutoConvert(Entity destination, Entity item)
    {
        if (destination.Type != "player")
        {
            return false;
        }
        if (!item.HasTag(CurrencyProperties.CurrencyTag))
        {
            return false;
        }
        var value = GetItemValue(item);
        if (value <= 0)
        {
            return false;
        }

        destination.RemoveFromContents(item);
        _world.UntrackEntity(item);
        var templateId = item.GetProperty<string>(CommonProperties.TemplateId) ?? item.Id.ToString();
        AddGold(destination, value, $"pickup:{templateId}");
        return true;
    }

    private static int GetItemValue(Entity item)
    {
        return item.GetProperty<object>(CurrencyProperties.Value) switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            string s when int.TryParse(s, out var n) => n,
            _ => 0
        };
    }

    public int AddGold(Entity entity, int delta, string reason)
    {
        var current = entity.GetProperty<int>(CurrencyProperties.Gold);
        var newTotal = Math.Max(0, current + delta);
        entity.SetProperty(CurrencyProperties.Gold, newTotal);

        var eventType = delta >= 0 ? "currency.credited" : "currency.debited";
        _eventBus.Publish(new GameEvent
        {
            Type = eventType,
            SourceEntityId = entity.Id,
            Data =
            {
                ["playerId"] = entity.Id,
                ["amount"] = Math.Abs(delta),
                ["source"] = entity.Id,
                ["reason"] = reason
            }
        });
        return newTotal;
    }

    public int SetGold(Entity entity, int amount, string reason)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Gold cannot be negative.");
        }
        entity.SetProperty(CurrencyProperties.Gold, amount);
        _eventBus.Publish(new GameEvent
        {
            Type = "currency.credited",
            SourceEntityId = entity.Id,
            Data =
            {
                ["playerId"] = entity.Id,
                ["amount"] = amount,
                ["source"] = entity.Id,
                ["reason"] = reason
            }
        });
        return amount;
    }
}
