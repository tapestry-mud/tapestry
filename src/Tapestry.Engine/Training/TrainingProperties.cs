using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Training;

public static class TrainingProperties
{
    public const string TrainsAvailable = "trains_available";
    public const string TrainerConfigKey = "trainer_config";

    public static void Register(PropertyTypeRegistry registry)
    {
        registry.Register(TrainsAvailable, typeof(int));
    }
}
