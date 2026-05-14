using Tapestry.Engine.Persistence;
using Tapestry.Engine;

namespace Tapestry.Engine.Items;

public static class ItemProperties
{
    public const string Rarity = "rarity";
    public const string Essence = "essence";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(Rarity, "Item rarity tier", PropertyValueType.String, appliesTo: new[] { EntityTypes.Item });
        registry.RegisterEngineProperty(Essence, "Magical essence type", PropertyValueType.String, appliesTo: new[] { EntityTypes.Item });
    }
}
