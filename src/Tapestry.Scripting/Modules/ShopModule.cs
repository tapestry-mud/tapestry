using Tapestry.Engine;
using Tapestry.Engine.Economy;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class ShopModule : IJintApiModule
{
    private readonly World _world;
    private readonly ShopService _shop;

    public string Namespace => "shop";

    public ShopModule(World world, ShopService shop)
    {
        _world = world;
        _shop = shop;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            isShop = new Func<string, bool>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return false; }
                var entity = _world.GetEntity(id);
                return entity != null && _shop.IsShop(entity);
            }),

            findShopInRoom = new Func<string, string?>((playerIdStr) =>
            {
                if (!Guid.TryParse(playerIdStr, out var id)) { return null; }
                var player = _world.GetEntity(id);
                if (player == null) { return null; }
                return _shop.FindShopInRoom(player)?.Id.ToString();
            }),

            listings = new Func<string, object[]>((npcIdStr) =>
            {
                if (!Guid.TryParse(npcIdStr, out var id)) { return Array.Empty<object>(); }
                var npc = _world.GetEntity(id);
                if (npc == null) { return Array.Empty<object>(); }
                return _shop.GetListings(npc)
                    .Select(l => (object)new
                    {
                        templateId = l.TemplateId,
                        name = l.Name,
                        price = l.BuyPrice
                    })
                    .ToArray();
            }),

            buy = new Func<string, string, string, object>((playerIdStr, npcIdStr, itemQuery) =>
            {
                if (!Guid.TryParse(playerIdStr, out var playerId) || !Guid.TryParse(npcIdStr, out var npcId))
                {
                    return new { ok = false, reason = "noShopHere", itemId = (string?)null, itemName = (string?)null, amount = 0L, goldRemaining = 0L };
                }
                var player = _world.GetEntity(playerId);
                var npc = _world.GetEntity(npcId);
                if (player == null || npc == null)
                {
                    return new { ok = false, reason = "noShopHere", itemId = (string?)null, itemName = (string?)null, amount = 0L, goldRemaining = 0L };
                }
                var result = _shop.Buy(player, npc, itemQuery);
                return new
                {
                    ok = result.Reason == ShopReason.Ok,
                    reason = ToCamelCase(result.Reason.ToString()),
                    itemId = result.ItemId,
                    itemName = result.ItemId != null && Guid.TryParse(result.ItemId, out var itemGuid)
                        ? _world.GetEntity(itemGuid)?.Name
                        : null,
                    amount = result.Price,
                    goldRemaining = result.PlayerGold
                };
            }),

            sell = new Func<string, string, string, object>((playerIdStr, npcIdStr, itemQuery) =>
            {
                if (!Guid.TryParse(playerIdStr, out var playerId) || !Guid.TryParse(npcIdStr, out var npcId))
                {
                    return new { ok = false, reason = "noShopHere", itemName = (string?)null, amount = 0L, goldRemaining = 0L };
                }
                var player = _world.GetEntity(playerId);
                var npc = _world.GetEntity(npcId);
                if (player == null || npc == null)
                {
                    return new { ok = false, reason = "noShopHere", itemName = (string?)null, amount = 0L, goldRemaining = 0L };
                }
                var result = _shop.Sell(player, npc, itemQuery);
                return new
                {
                    ok = result.Reason == ShopReason.Ok,
                    reason = ToCamelCase(result.Reason.ToString()),
                    itemName = result.ItemName,
                    amount = result.Price,
                    goldRemaining = result.PlayerGold
                };
            }),

            value = new Func<string, string, string, object>((playerIdStr, npcIdStr, itemQuery) =>
            {
                if (!Guid.TryParse(playerIdStr, out var playerId) || !Guid.TryParse(npcIdStr, out var npcId))
                {
                    return new { ok = false, reason = "noShopHere", itemName = (string?)null, amount = 0L, scope = "stock" };
                }
                var player = _world.GetEntity(playerId);
                var npc = _world.GetEntity(npcId);
                if (player == null || npc == null)
                {
                    return new { ok = false, reason = "noShopHere", itemName = (string?)null, amount = 0L, scope = "stock" };
                }
                var result = _shop.Value(player, npc, itemQuery);
                return new
                {
                    ok = result.Reason == ShopReason.Ok,
                    reason = ToCamelCase(result.Reason.ToString()),
                    itemName = result.ItemName,
                    amount = result.Price,
                    scope = result.Scope == ValueScope.Inventory ? "inventory" : "stock"
                };
            })
        };
    }

    private static string ToCamelCase(string s)
    {
        if (string.IsNullOrEmpty(s)) { return s; }
        return char.ToLowerInvariant(s[0]) + s[1..];
    }
}
