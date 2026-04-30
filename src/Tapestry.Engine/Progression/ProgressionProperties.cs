// See also: CommonProperties.cs for shared entity properties
using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Progression;

/// <summary>
/// Property keys for the progression/leveling system.
/// </summary>
public static class ProgressionProperties
{
    /// <summary>Key prefix: "level:"</summary>
    public const string LevelPrefix = "level:";

    /// <summary>Key prefix: "xp:"</summary>
    public const string XpPrefix = "xp:";

    /// <summary>Key: "level:{trackName}"</summary>
    public static string Level(string trackName) => $"level:{trackName}";

    /// <summary>Key: "xp:{trackName}"</summary>
    public static string Xp(string trackName) => $"xp:{trackName}";

    public static void Register(PropertyTypeRegistry registry)
    {
        registry.RegisterPrefix(LevelPrefix, typeof(int));
        registry.RegisterPrefix(XpPrefix, typeof(int));
    }
}
