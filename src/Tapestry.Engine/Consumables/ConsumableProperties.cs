using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Consumables;

public static class ConsumableProperties
{
    public const string ItemType = "item_type";
    public const string SustenanceValue = "sustenance_value";
    public const string EffectId = "effect_id";
    public const string EffectDuration = "effect_duration";
    public const string EffectData = "effect_data";
    public const string Charges = "charges";
    public const string MaxCharges = "max_charges";
    public const string DestroyOnEmpty = "destroy_on_empty";

    public static void Register(PropertyTypeRegistry registry)
    {
        registry.Register(ItemType, typeof(string));
        registry.Register(SustenanceValue, typeof(int));
        registry.Register(EffectId, typeof(string));
        registry.Register(EffectDuration, typeof(int));
        registry.Register(EffectData, typeof(Dictionary<string, object>));
        registry.Register(Charges, typeof(int));
        registry.Register(MaxCharges, typeof(int));
        registry.Register(DestroyOnEmpty, typeof(bool));
    }
}
