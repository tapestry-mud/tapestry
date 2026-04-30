namespace Tapestry.Engine.Economy;

public class EconomyConfig
{
    public double ShopBuyMarkup { get; private set; } = 1.2;
    public double ShopSellDiscount { get; private set; } = 0.5;

    public void Configure(double shopBuyMarkup, double shopSellDiscount)
    {
        ShopBuyMarkup = shopBuyMarkup;
        ShopSellDiscount = shopSellDiscount;
    }
}
