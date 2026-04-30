using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Economy;

public static class CurrencyProperties
{
    public const string Gold = "gold";
    public const string Value = "value";
    public const string CurrencyTag = "currency";
    public const string NoSellTag = "no_sell";

    public static void Register(PropertyTypeRegistry registry)
    {
        registry.Register(Gold, typeof(int));
        registry.Register(Value, typeof(int));
    }
}
