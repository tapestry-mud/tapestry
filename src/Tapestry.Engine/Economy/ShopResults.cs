namespace Tapestry.Engine.Economy;

public enum ShopReason
{
    Ok,
    NoShopHere,
    ItemNotForSale,
    InsufficientGold,
    ItemNotInInventory,
    ItemIsNoSell,
    ItemValueZero,
    AmbiguousItem
}

public enum ValueScope { Stock, Inventory }

public sealed record ShopListing(string TemplateId, string Name, long BuyPrice);
public sealed record ShopBuyResult(ShopReason Reason, string? ItemId, long Price, long PlayerGold);
public sealed record ShopSellResult(ShopReason Reason, string? ItemName, long Price, long PlayerGold);
public sealed record ShopValueResult(ShopReason Reason, string? ItemName, long Price, ValueScope Scope);
