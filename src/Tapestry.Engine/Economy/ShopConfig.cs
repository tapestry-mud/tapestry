namespace Tapestry.Engine.Economy;

public record ShopConfig(
    IReadOnlyList<string> Sells,
    double BuyModifier,
    double SellModifier
);
