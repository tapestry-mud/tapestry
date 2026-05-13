namespace Tapestry.Engine.Economy;

public record ShopConfig(
    IReadOnlyList<string> Sells,
    double BuyMarkup,
    double SellDiscount
);
