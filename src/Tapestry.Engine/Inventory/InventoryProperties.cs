using Tapestry.Engine.Persistence;
using Tapestry.Engine;

namespace Tapestry.Engine.Inventory;

public static class InventoryProperties
{
    public const string Slot = "slot";
    public const string Weight = "weight";
    public const string MaxCarryWeight = "max_carry_weight";
    public const string Modifiers = "modifiers";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(Slot, "Equipment slot this item occupies", PropertyValueType.String, appliesTo: new[] { EntityTypes.Item });
        registry.RegisterEngineProperty(Weight, "Weight of this entity", PropertyValueType.Double);
        registry.RegisterEngineProperty(MaxCarryWeight, "Maximum carry weight", PropertyValueType.Double, appliesTo: new[] { EntityTypes.Player, EntityTypes.Npc });
        registry.RegisterEngineProperty(Modifiers, "Stat modifiers granted by this item", PropertyValueType.String, transient: true);
    }
}
