using Tapestry.Engine.Persistence;
using Tapestry.Engine;

namespace Tapestry.Engine.Sustenance;

public static class SustenanceProperties
{
    public const string Sustenance = "sustenance";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(Sustenance, "Current sustenance level", PropertyValueType.Int, appliesTo: new[] { EntityTypes.Player, EntityTypes.Npc });
    }
}
