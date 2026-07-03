using Tapestry.Engine.Persistence;
using Tapestry.Engine;

namespace Tapestry.Engine.Combat;

public static class CombatProperties
{
    public const string DamageType = "damage_type";
    public const string DamageDice = "damage_dice";
    public const string HitBonus = "hit_bonus";
    public const string CombatName = "combat_name";
    public const string WimpyPct = "wimpy_pct";
    public const string AttackSpeed = "attack_speed";
    public const string ArmorClass = "ac";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(DamageType, "Damage type dealt by this entity", PropertyValueType.String, appliesTo: new[] { EntityTypes.Item, EntityTypes.Npc });
        registry.RegisterEngineProperty(DamageDice, "Damage dice expression (e.g. 2d6+4)", PropertyValueType.String, appliesTo: new[] { EntityTypes.Item, EntityTypes.Npc });
        registry.RegisterEngineProperty(HitBonus, "Bonus to hit rolls", PropertyValueType.Int, appliesTo: new[] { EntityTypes.Item, EntityTypes.Npc });
        registry.RegisterEngineProperty(CombatName, "Verb used in combat messages", PropertyValueType.String, appliesTo: new[] { EntityTypes.Item, EntityTypes.Npc });
        registry.RegisterEngineProperty(WimpyPct, "HP percentage (0-100) at or below which entity auto-flees", PropertyValueType.Int, appliesTo: new[] { EntityTypes.Player, EntityTypes.Npc });
        registry.RegisterEngineProperty(AttackSpeed, "Attack speed modifier", PropertyValueType.Int, appliesTo: new[] { EntityTypes.Item, EntityTypes.Npc });
        registry.RegisterEngineProperty(ArmorClass, "Armor class by damage type",
            PropertyValueType.MapInt, appliesTo: new[] { EntityTypes.Item, EntityTypes.Npc });
    }
}
