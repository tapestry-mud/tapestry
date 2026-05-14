using Tapestry.Engine.Persistence;
using Tapestry.Engine;

namespace Tapestry.Engine.Consumables;

public static class ConsumableProperties
{
    public const string ConsumeMethod = "consume_method";
    public const string SustenanceValue = "sustenance_value";
    public const string EffectId = "effect_id";
    public const string EffectDuration = "effect_duration";
    public const string EffectData = "effect_data";
    public const string Charges = "charges";
    public const string MaxCharges = "max_charges";
    public const string DestroyOnEmpty = "destroy_on_empty";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(ConsumeMethod, "Consumption method", PropertyValueType.String, appliesTo: new[] { EntityTypes.Item });
        registry.RegisterEngineProperty(SustenanceValue, "Sustenance provided when consumed", PropertyValueType.Int, appliesTo: new[] { EntityTypes.Item });
        registry.RegisterEngineProperty(EffectId, "Effect applied when consumed", PropertyValueType.String, appliesTo: new[] { EntityTypes.Item });
        registry.RegisterEngineProperty(EffectDuration, "Duration of applied effect in ticks", PropertyValueType.Int, appliesTo: new[] { EntityTypes.Item });
        registry.RegisterEngineProperty(EffectData, "Effect parameters", PropertyValueType.MapInt, appliesTo: new[] { EntityTypes.Item }, transient: true);
        registry.RegisterEngineProperty(Charges, "Remaining charges", PropertyValueType.Int, appliesTo: new[] { EntityTypes.Item });
        registry.RegisterEngineProperty(MaxCharges, "Maximum charges", PropertyValueType.Int, appliesTo: new[] { EntityTypes.Item });
        registry.RegisterEngineProperty(DestroyOnEmpty, "Destroy item when charges reach zero", PropertyValueType.Bool, appliesTo: new[] { EntityTypes.Item });
    }
}
