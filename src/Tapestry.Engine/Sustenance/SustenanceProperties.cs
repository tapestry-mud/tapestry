using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Sustenance;

public static class SustenanceProperties
{
    public const string Sustenance = "sustenance";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(Sustenance, "Current sustenance level", PropertyValueType.Int, appliesTo: new[] { "player", "npc" });
    }
}
