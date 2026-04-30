using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Sustenance;

public static class SustenanceProperties
{
    public const string Sustenance = "sustenance";

    public static void Register(PropertyTypeRegistry registry)
    {
        registry.Register(Sustenance, typeof(int));
    }
}
