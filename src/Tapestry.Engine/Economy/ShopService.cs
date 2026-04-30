using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Shared;

namespace Tapestry.Engine.Economy;

public class ShopService
{
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly CurrencyService _currency;
    private readonly EconomyConfig _economyConfig;
    private readonly ItemRegistry _itemRegistry;
    private readonly EquipmentManager _equipmentManager;

    public ShopService(
        World world,
        EventBus eventBus,
        CurrencyService currency,
        EconomyConfig economyConfig,
        ItemRegistry itemRegistry,
        EquipmentManager equipmentManager)
    {
        _world = world;
        _eventBus = eventBus;
        _currency = currency;
        _economyConfig = economyConfig;
        _itemRegistry = itemRegistry;
        _equipmentManager = equipmentManager;
    }

    public bool IsShop(Entity npc) => npc.HasTag(ShopProperties.ShopTag);

    public Entity? FindShopInRoom(Entity player)
    {
        if (player.LocationRoomId == null)
        {
            return null;
        }
        return _world.GetEntitiesInRoom(player.LocationRoomId)
            .FirstOrDefault(e => e.HasTag(ShopProperties.ShopTag));
    }

    public IReadOnlyList<ShopListing> GetListings(Entity npc)
    {
        var sells = npc.GetProperty<List<string>>(ShopProperties.Sells);
        if (sells == null)
        {
            return Array.Empty<ShopListing>();
        }

        var listings = new List<ShopListing>();
        foreach (var templateId in sells)
        {
            var template = _itemRegistry.GetTemplate(templateId);
            if (template == null)
            {
                continue;
            }
            var rawValue = template.Properties.TryGetValue(CurrencyProperties.Value, out var v)
                ? Convert.ToInt32(v)
                : 0;
            if (rawValue <= 0)
            {
                continue;
            }
            var price = ComputeBuyPrice(npc, rawValue);
            listings.Add(new ShopListing(templateId, template.Name, price));
        }
        return listings;
    }

    public long ComputeBuyPrice(Entity npc, int itemValue)
    {
        double markup;
        var overrideRaw = npc.GetProperty<object>(ShopProperties.BuyMarkup);
        markup = overrideRaw != null
            ? Convert.ToDouble(overrideRaw)
            : _economyConfig.ShopBuyMarkup;
        return Math.Max(1L, (long)Math.Round(itemValue * markup));
    }

    public long ComputeSellPrice(Entity npc, int itemValue)
    {
        double discount;
        var overrideRaw = npc.GetProperty<object>(ShopProperties.SellDiscount);
        discount = overrideRaw != null
            ? Convert.ToDouble(overrideRaw)
            : _economyConfig.ShopSellDiscount;
        return Math.Max(1L, (long)Math.Round(itemValue * discount));
    }

    public ShopBuyResult Buy(Entity player, Entity npc, string itemQuery)
    {
        var playerGold = player.GetProperty<int>(CurrencyProperties.Gold);

        var (templateId, templateName, rawValue) = ResolveStockItem(npc, itemQuery);
        if (templateId == null)
        {
            return new ShopBuyResult(ShopReason.ItemNotForSale, null, 0, playerGold);
        }

        var price = ComputeBuyPrice(npc, rawValue);
        if (playerGold < price)
        {
            return new ShopBuyResult(ShopReason.InsufficientGold, null, price, playerGold);
        }

        var buyEvent = new GameEvent
        {
            Type = "shop.buy",
            SourceEntityId = player.Id,
            TargetEntityId = npc.Id,
            Data =
            {
                ["playerId"] = player.Id,
                ["npcId"] = npc.Id,
                ["itemTemplateId"] = templateId,
                ["amount"] = price
            }
        };
        _eventBus.Publish(buyEvent);
        if (buyEvent.Cancelled)
        {
            return new ShopBuyResult(ShopReason.ItemNotForSale, null, price, playerGold);
        }

        _currency.AddGold(player, -(int)price, $"shop:buy:{templateId}");
        var item = _itemRegistry.CreateItem(templateId);
        if (item == null)
        {
            return new ShopBuyResult(ShopReason.ItemNotForSale, null, price, playerGold);
        }
        _world.TrackEntity(item);
        player.AddToContents(item);
        var newGold = player.GetProperty<int>(CurrencyProperties.Gold);
        return new ShopBuyResult(ShopReason.Ok, item.Id.ToString(), price, newGold);
    }

    public ShopSellResult Sell(Entity player, Entity npc, string itemQuery)
    {
        var item = ResolveInventoryItem(player, itemQuery);
        if (item == null)
        {
            return new ShopSellResult(ShopReason.ItemNotInInventory, null, 0, player.GetProperty<int>(CurrencyProperties.Gold));
        }

        if (item.HasTag(CurrencyProperties.NoSellTag))
        {
            return new ShopSellResult(ShopReason.ItemIsNoSell, item.Name, 0, player.GetProperty<int>(CurrencyProperties.Gold));
        }

        var rawValue = GetEntityValue(item);
        if (rawValue <= 0)
        {
            return new ShopSellResult(ShopReason.ItemValueZero, item.Name, 0, player.GetProperty<int>(CurrencyProperties.Gold));
        }

        var price = ComputeSellPrice(npc, rawValue);

        var sellEvent = new GameEvent
        {
            Type = "shop.sell",
            SourceEntityId = player.Id,
            TargetEntityId = npc.Id,
            Data =
            {
                ["playerId"] = player.Id,
                ["npcId"] = npc.Id,
                ["itemId"] = item.Id,
                ["amount"] = price
            }
        };
        _eventBus.Publish(sellEvent);
        if (sellEvent.Cancelled)
        {
            return new ShopSellResult(ShopReason.ItemNotForSale, item.Name, price, player.GetProperty<int>(CurrencyProperties.Gold));
        }

        // Auto-unequip if worn/wielded
        var equippedSlot = player.Equipment
            .FirstOrDefault(kv => kv.Value.Id == item.Id).Key;
        if (equippedSlot != null)
        {
            _equipmentManager.Unequip(player, equippedSlot, silent: true);
        }

        player.RemoveFromContents(item);
        _world.UntrackEntity(item);
        _currency.AddGold(player, (int)price, $"shop:sell:{item.Id}");
        var newGold = player.GetProperty<int>(CurrencyProperties.Gold);
        return new ShopSellResult(ShopReason.Ok, item.Name, price, newGold);
    }

    public ShopValueResult Value(Entity player, Entity npc, string itemQuery)
    {
        // Inventory-first: check if player has item matching query
        var inventoryItem = ResolveInventoryItem(player, itemQuery);
        if (inventoryItem != null)
        {
            var rawValue = GetEntityValue(inventoryItem);
            var sellPrice = rawValue > 0
                ? ComputeSellPrice(npc, rawValue)
                : 0;
            return new ShopValueResult(ShopReason.Ok, inventoryItem.Name, sellPrice, ValueScope.Inventory);
        }

        // Stock fallback: check shop stock
        var (templateId, templateName, stockRawValue) = ResolveStockItem(npc, itemQuery);
        if (templateId != null)
        {
            var buyPrice = ComputeBuyPrice(npc, stockRawValue);
            return new ShopValueResult(ShopReason.Ok, templateName, buyPrice, ValueScope.Stock);
        }

        return new ShopValueResult(ShopReason.ItemNotForSale, null, 0, ValueScope.Stock);
    }

    private (string? templateId, string? name, int value) ResolveStockItem(Entity npc, string query)
    {
        var sells = npc.GetProperty<List<string>>(ShopProperties.Sells);
        if (sells == null)
        {
            return (null, null, 0);
        }

        var normalizedQuery = StripArticle(query).ToLowerInvariant().Replace('-', ' ');
        (string templateId, string name, int value)? match = null;

        foreach (var templateId in sells)
        {
            var template = _itemRegistry.GetTemplate(templateId);
            if (template == null)
            {
                continue;
            }

            var shortForm = templateId.Contains(':')
                ? templateId[(templateId.LastIndexOf(':') + 1)..]
                : templateId;
            var templateName = StripArticle(template.Name).ToLowerInvariant();
            var shortFormNormalized = shortForm.Replace('-', ' ').ToLowerInvariant();

            if (!templateName.StartsWith(normalizedQuery) && !shortFormNormalized.StartsWith(normalizedQuery))
            {
                continue;
            }

            var rawValue = template.Properties.TryGetValue(CurrencyProperties.Value, out var v)
                ? Convert.ToInt32(v)
                : 0;

            if (match.HasValue)
            {
                return (null, null, 0); // ambiguous
            }
            match = (templateId, template.Name, rawValue);
        }

        return match.HasValue ? (match.Value.templateId, match.Value.name, match.Value.value) : (null, null, 0);
    }

    private Entity? ResolveInventoryItem(Entity player, string query)
    {
        var normalizedQuery = StripArticle(query).ToLowerInvariant();
        return player.Contents.FirstOrDefault(item =>
        {
            var itemName = StripArticle(item.Name).ToLowerInvariant();
            return itemName.StartsWith(normalizedQuery);
        });
    }

    // Handles int, long, double from YAML; returns 0 for anything else (dict, null, etc.)
    private static int GetEntityValue(Entity entity)
    {
        return entity.GetProperty<object>(CurrencyProperties.Value) switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            string s when int.TryParse(s, out var n) => n,
            _ => 0
        };
    }

    private static string StripArticle(string name)
    {
        foreach (var article in new[] { "a ", "an ", "the " })
        {
            if (name.StartsWith(article, StringComparison.OrdinalIgnoreCase))
            {
                return name[article.Length..];
            }
        }
        return name;
    }
}
