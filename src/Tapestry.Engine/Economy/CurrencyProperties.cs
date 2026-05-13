using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Economy;

public static class CurrencyProperties
{
    public const string Gold = "gold";
    public const string Value = "value";
    public const string CurrencyTag = "currency";
    public const string NoSellTag = "no_sell";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(Gold, "Currency held by this entity", PropertyValueType.Int);
    }
}
