using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Mobs;

public static class MobProperties
{
    public const string Behavior = "behavior";
    public const string Script = "script";
    public const string Gender = "gender";
    public const string LastIp = "last_ip";
    public const string ReturnRoom = "return_room";

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
    }
}
