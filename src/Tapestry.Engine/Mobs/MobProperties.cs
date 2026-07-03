using Tapestry.Engine.Persistence;
using Tapestry.Engine;

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
    public const string GoldMin = "gold_min";
    public const string GoldMax = "gold_max";
    public const string MobLevel = "mob_level";
    public const string IdleCommands = "idle_commands";
    public const string IdleChance = "idle_chance";
    public const string IdleInterval = "idle_interval";
    public const string BattleCommands = "battle_commands";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(Behavior, "Mob behavior controller (stationary, wanderer, etc.)",
            PropertyValueType.String, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(Script, "Script file path for this mob",
            PropertyValueType.String, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(Gender, "Entity gender string",
            PropertyValueType.String, appliesTo: new[] { EntityTypes.Player, EntityTypes.Npc });
        registry.RegisterEngineProperty(LastIp, "Last login IP address",
            PropertyValueType.String, appliesTo: new[] { EntityTypes.Player }, transient: true);
        registry.RegisterEngineProperty(ReturnRoom, "Room to return to after transient teleport",
            PropertyValueType.String, appliesTo: new[] { EntityTypes.Player }, transient: true);
        registry.RegisterEngineProperty(PatrolRoute, "Ordered list of room IDs for patrol behavior",
            PropertyValueType.ListString, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(PatrolInterval, "Seconds between patrol movements",
            PropertyValueType.Int, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(ShopSells, "List of item IDs this shop sells",
            PropertyValueType.ListString, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(BattleInterval, "Seconds between NPC combat actions",
            PropertyValueType.Int, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(BattleChance, "Probability of executing a battle action per tick",
            PropertyValueType.Double, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(WanderInterval, "Seconds between wander movements",
            PropertyValueType.Int, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(WanderBoundary, "Scope of wandering behavior (area, zone, etc.)",
            PropertyValueType.String, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(WanderChance, "Probability of moving each wander tick (0.0-1.0)",
            PropertyValueType.Double, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(PatrolChance, "Probability of advancing patrol each tick (0.0-1.0)",
            PropertyValueType.Double, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(GoldMin, "Minimum gold dropped on death",
            PropertyValueType.Int, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(GoldMax, "Maximum gold dropped on death",
            PropertyValueType.Int, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(MobLevel, "Fixed combat level of this NPC",
            PropertyValueType.Int, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(IdleCommands, "Commands executed randomly when idle",
            PropertyValueType.ListString, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(IdleChance, "Probability of executing an idle command per check (0.0-1.0)",
            PropertyValueType.Double, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(IdleInterval, "Seconds between idle command checks",
            PropertyValueType.Int, appliesTo: new[] { EntityTypes.Npc });
        registry.RegisterEngineProperty(BattleCommands, "Commands executed randomly during combat",
            PropertyValueType.ListString, appliesTo: new[] { EntityTypes.Npc });
    }
}
