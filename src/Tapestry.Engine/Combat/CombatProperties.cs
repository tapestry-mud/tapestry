using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Combat;

public static class CombatProperties
{
    public const string DamageType = "damage_type";
    public const string DamageDice = "damage_dice";
    public const string HitBonus = "hit_bonus";
    public const string CombatName = "combat_name";
    public const string WimpyThreshold = "wimpy_threshold";
    public const string AttackSpeed = "attack_speed";
    public const string ArmorClass = "ac";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(DamageType, "Damage type dealt by this entity", PropertyValueType.String);
        registry.RegisterEngineProperty(DamageDice, "Damage dice expression (e.g. 2d6+4)", PropertyValueType.String);
        registry.RegisterEngineProperty(HitBonus, "Bonus to hit rolls", PropertyValueType.Int);
        registry.RegisterEngineProperty(CombatName, "Verb used in combat messages", PropertyValueType.String);
        registry.RegisterEngineProperty(WimpyThreshold, "HP below which entity flees", PropertyValueType.Int);
        registry.RegisterEngineProperty(AttackSpeed, "Attack speed modifier", PropertyValueType.Int);
        registry.RegisterEngineProperty(ArmorClass, "Armor class by damage type",
            PropertyValueType.MapInt);
    }
}
