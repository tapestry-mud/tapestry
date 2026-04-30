// See also: CommonProperties.cs for shared entity properties
using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Items;

public static class ItemProperties
{
    public const string Rarity = "rarity";
    public const string Essence = "essence";

    public static void Register(PropertyTypeRegistry registry)
    {
        registry.Register(Rarity, typeof(string));
        registry.Register(Essence, typeof(string));
    }
}
