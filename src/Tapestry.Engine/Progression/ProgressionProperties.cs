using Tapestry.Engine.Persistence;
using Tapestry.Engine;

namespace Tapestry.Engine.Progression;

public static class ProgressionProperties
{
    public const string Level = "level";
    public const string Xp = "xp";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(Level, "Level by progression track",
            PropertyValueType.MapInt, appliesTo: new[] { EntityTypes.Player, EntityTypes.Npc });
        registry.RegisterEngineProperty(Xp, "Experience points by progression track",
            PropertyValueType.MapInt, appliesTo: new[] { EntityTypes.Player, EntityTypes.Npc });
    }
}
