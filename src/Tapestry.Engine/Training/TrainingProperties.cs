using Tapestry.Engine.Persistence;
using Tapestry.Engine;

namespace Tapestry.Engine.Training;

public static class TrainingProperties
{
    public const string TrainsAvailable = "trains_available";
    public const string TrainerConfigKey = "trainer_config";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(TrainsAvailable, "Number of training sessions available",
            PropertyValueType.Int, appliesTo: new[] { EntityTypes.Player });
    }
}
