using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Abilities;

public static class AbilityProperties
{
    public const string Proficiency = "proficiency";
    public const string Cap = "cap";
    public const string QueuedActions = "queued_actions";
    public const string LastAbilityUsed = "last_ability_used";

    public static string ProficiencyKey(string abilityId) => abilityId;
    public static string CapKey(string abilityId) => abilityId;

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(Proficiency, "Ability proficiency scores",
            PropertyValueType.MapInt, appliesTo: new[] { "player", "npc" });
        registry.RegisterEngineProperty(Cap, "Ability proficiency caps",
            PropertyValueType.MapInt, appliesTo: new[] { "player", "npc" });
        registry.RegisterEngineProperty(LastAbilityUsed, "Last ability used by this entity",
            PropertyValueType.String, transient: true);
        registry.RegisterEngineProperty(QueuedActions, "Queued ability actions",
            PropertyValueType.String, transient: true);
    }
}
