using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Mobs;

public static class MobProperties
{
    public const string Behavior = "behavior";
    public const string Script = "script";
    public const string Gender = "gender";
    public const string LastIp = "last_ip";
    public const string ReturnRoom = "return_room";
    public const string PatrolRoute = "patrol_route";
    public const string PatrolInterval = "patrol_interval";
    public const string ShopSells = "shop_sells";
    public const string BattleInterval = "battle_interval";
    public const string BattleChance = "battle_chance";
    public const string WanderInterval = "wander_interval";
    public const string WanderBoundary = "wander_boundary";
    public const string WanderChance = "wander_chance";
    public const string PatrolChance = "patrol_chance";
    public const string FleeThreshold = "flee_threshold";
    public const string GoldMin = "gold_min";
    public const string GoldMax = "gold_max";
    public const string MobLevel = "mob_level";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(Behavior, "Mob behavior controller (stationary, wanderer, etc.)",
            PropertyValueType.String, appliesTo: new[] { "npc" });
        registry.RegisterEngineProperty(Script, "Script file path for this mob",
            PropertyValueType.String, appliesTo: new[] { "npc" });
        registry.RegisterEngineProperty(Gender, "Entity gender string",
            PropertyValueType.String, appliesTo: new[] { "player", "npc" });
        registry.RegisterEngineProperty(LastIp, "Last login IP address",
            PropertyValueType.String, appliesTo: new[] { "player" }, transient: true);
        registry.RegisterEngineProperty(ReturnRoom, "Room to return to after transient teleport",
            PropertyValueType.String, appliesTo: new[] { "player" }, transient: true);
        registry.RegisterEngineProperty(PatrolRoute, "Ordered list of room IDs for patrol behavior",
            PropertyValueType.ListString, appliesTo: new[] { "npc" });
        registry.RegisterEngineProperty(PatrolInterval, "Seconds between patrol movements",
            PropertyValueType.Int, appliesTo: new[] { "npc" });
        registry.RegisterEngineProperty(ShopSells, "List of item IDs this shop sells",
            PropertyValueType.ListString, appliesTo: new[] { "npc" });
        registry.RegisterEngineProperty(BattleInterval, "Seconds between NPC combat actions",
            PropertyValueType.Int, appliesTo: new[] { "npc" });
        registry.RegisterEngineProperty(BattleChance, "Probability of executing a battle action per tick",
            PropertyValueType.Double, appliesTo: new[] { "npc" });
        registry.RegisterEngineProperty(WanderInterval, "Seconds between wander movements",
            PropertyValueType.Int, appliesTo: new[] { "npc" });
        registry.RegisterEngineProperty(WanderBoundary, "Scope of wandering behavior (area, zone, etc.)",
            PropertyValueType.String, appliesTo: new[] { "npc" });
        registry.RegisterEngineProperty(WanderChance, "Probability of moving each wander tick (0.0-1.0)",
            PropertyValueType.Double, appliesTo: new[] { "npc" });
        registry.RegisterEngineProperty(PatrolChance, "Probability of advancing patrol each tick (0.0-1.0)",
            PropertyValueType.Double, appliesTo: new[] { "npc" });
        registry.RegisterEngineProperty(FleeThreshold, "HP percentage threshold below which mob flees (0.0-1.0)",
            PropertyValueType.Double, appliesTo: new[] { "npc" });
        registry.RegisterEngineProperty(GoldMin, "Minimum gold dropped on death",
            PropertyValueType.Int, appliesTo: new[] { "npc" });
        registry.RegisterEngineProperty(GoldMax, "Maximum gold dropped on death",
            PropertyValueType.Int, appliesTo: new[] { "npc" });
        registry.RegisterEngineProperty(MobLevel, "Fixed combat level of this NPC",
            PropertyValueType.Int, appliesTo: new[] { "npc" });
    }
}
