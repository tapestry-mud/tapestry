using Tapestry.Engine.Persistence;
using Tapestry.Engine;

namespace Tapestry.Engine.Items;

public static class ItemProperties
{
    public const string Rarity = "rarity";
    public const string Essence = "essence";
    public const string DistributedFrom = "distributed_from";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(Rarity, "Item rarity tier", PropertyValueType.String, appliesTo: new[] { EntityTypes.Item });
        registry.RegisterEngineProperty(Essence, "Magical essence type", PropertyValueType.String, appliesTo: new[] { EntityTypes.Item });
        registry.RegisterEngineProperty(DistributedFrom, "Template id this distributed item instance was spawned from; used for replenish top-up counting", PropertyValueType.String, appliesTo: new[] { EntityTypes.Item }, settable: false);
    }
}
