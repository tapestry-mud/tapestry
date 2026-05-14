using Tapestry.Engine.Persistence;

namespace Tapestry.Engine;

public static class RoomProperties
{
    public const string Terrain = "terrain";
    public const string EntryPointDescription = "entry_point_description";
    public const string EntryPointDirection = "entry_point_direction";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(Terrain, "Terrain type of this room", PropertyValueType.String,
            appliesTo: new[] { EntityTypes.Room });
        registry.RegisterEngineProperty(EntryPointDescription, "Description shown when entering from another area",
            PropertyValueType.String, appliesTo: new[] { EntityTypes.Room });
        registry.RegisterEngineProperty(EntryPointDirection, "Direction word used for area entry point",
            PropertyValueType.String, appliesTo: new[] { EntityTypes.Room });
    }
}
