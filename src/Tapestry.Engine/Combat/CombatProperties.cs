// See also: CommonProperties.cs for shared entity properties
using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Combat;

/// <summary>
/// Property keys for the combat system.
/// </summary>
public static class CombatProperties
{
    /// <summary>Key: "damage_type"</summary>
    public const string DamageType = "damage_type";

    /// <summary>Key: "damage_dice"</summary>
    public const string DamageDice = "damage_dice";

    /// <summary>Key: "hit_bonus"</summary>
    public const string HitBonus = "hit_bonus";

    /// <summary>Key: "combat_name"</summary>
    public const string CombatName = "combat_name";

    /// <summary>Key: "wimpy_threshold"</summary>
    public const string WimpyThreshold = "wimpy_threshold";

    /// <summary>Key: "attack_speed"</summary>
    public const string AttackSpeed = "attack_speed";

    /// <summary>Key prefix: "ac_"</summary>
    public const string ArmorClassPrefix = "ac_";

    /// <summary>Key: "ac_{damageType}"</summary>
    public static string ArmorClass(string damageType) => $"ac_{damageType}";

    public static void Register(PropertyTypeRegistry registry)
    {
        registry.Register(DamageType, typeof(string));
        registry.Register(DamageDice, typeof(string));
        registry.Register(HitBonus, typeof(int));
        registry.Register(CombatName, typeof(string));
        registry.Register(WimpyThreshold, typeof(int));
        registry.Register(AttackSpeed, typeof(int));
        registry.RegisterPrefix(ArmorClassPrefix, typeof(int));
    }
}
