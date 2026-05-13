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

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(RestState, "Current rest state (awake/resting/sleeping)", PropertyValueType.String, transient: true);
        registry.RegisterEngineProperty(RestTarget, "Entity being rested on or near", PropertyValueType.String, transient: true);
        registry.RegisterEngineProperty(RestBonus, "Bonus regen while resting", PropertyValueType.Int, transient: true);
        registry.RegisterEngineProperty(SleepStartTick, "Tick when sleep began", PropertyValueType.Long, transient: true);
        registry.RegisterEngineProperty(RoomHealingRate, "Healing rate bonus for this room", PropertyValueType.Int, appliesTo: new[] { "room" });
    }
}
