// See also: CommonProperties.cs for shared entity properties
using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Inventory;

/// <summary>
/// Property keys for the inventory and equipment system.
/// </summary>
public static class InventoryProperties
{
    /// <summary>Key: "slot"</summary>
    public const string Slot = "slot";

    /// <summary>Key: "weight"</summary>
    public const string Weight = "weight";

    /// <summary>Key: "max_carry_weight"</summary>
    public const string MaxCarryWeight = "max_carry_weight";

    /// <summary>Key: "modifiers"</summary>
    public const string Modifiers = "modifiers";

    public static void Register(PropertyTypeRegistry registry)
    {
        registry.Register(Slot, typeof(string));
        registry.Register(Weight, typeof(double));
        registry.Register(MaxCarryWeight, typeof(double));
        registry.Register(Modifiers, typeof(object), transient: true);
    }
}
