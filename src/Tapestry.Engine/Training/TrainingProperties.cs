using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Training;

public static class TrainingProperties
{
    public const string TrainsAvailable = "trains_available";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(TrainsAvailable, "Number of training sessions available",
            PropertyValueType.Int, appliesTo: new[] { "player" });
    }
}
