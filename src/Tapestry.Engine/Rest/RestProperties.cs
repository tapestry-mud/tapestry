using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Rest;

public static class RestProperties
{
    public const string RestState = "rest_state";
    public const string RestTarget = "rest_target";
    public const string RestBonus = "rest_bonus";
    public const string SleepStartTick = "sleep_start_tick";
    public const string RoomHealingRate = "healing_rate";

    public const string StateAwake = "awake";
    public const string StateResting = "resting";
    public const string StateSleeping = "sleeping";

    public static void Register(PropertyTypeRegistry registry)
    {
        registry.Register(RestState, typeof(string));
        registry.Register(RestTarget, typeof(string));
        registry.Register(RestBonus, typeof(int));
        registry.Register(SleepStartTick, typeof(long));
        registry.Register(RoomHealingRate, typeof(int));
    }
}
