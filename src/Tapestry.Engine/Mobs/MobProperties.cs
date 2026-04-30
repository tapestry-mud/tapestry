// See also: CommonProperties.cs for shared entity properties
using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Mobs;

/// <summary>
/// Property keys for mob behavior.
/// </summary>
public static class MobProperties
{
    /// <summary>Key: "behavior"</summary>
    public const string Behavior = "behavior";

    public static void Register(PropertyTypeRegistry registry)
    {
        registry.Register(Behavior, typeof(string));
    }
}
