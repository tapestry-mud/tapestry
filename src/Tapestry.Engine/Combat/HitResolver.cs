// src/Tapestry.Engine/Combat/HitResolver.cs
namespace Tapestry.Engine.Combat;

public record HitResult(bool IsHit, bool IsCritical, bool IsFumble, int NaturalRoll, int TotalRoll, int TargetAC);

public static class HitResolver
{
    public const string UnarmedDamageDice = "1d2+0";
    public const string UnarmedDamageType = "bash";
    private const int BaseAC = 10;

    public static string GetWeaponDamageType(Entity? weapon)
    {
        if (weapon == null)
        {
            return UnarmedDamageType;
        }

        var damageType = weapon.GetProperty<string>(CombatProperties.DamageType);
        return string.IsNullOrEmpty(damageType) ? UnarmedDamageType : damageType;
    }

    public static int CalculateHitRoll(Entity attacker, Entity? weapon)
    {
        var dexMod = attacker.Stats.Dexterity - 10;
        var hitBonus = 0;

        if (weapon != null)
        {
            var bonus = weapon.GetProperty<object>(CombatProperties.HitBonus);
            if (bonus != null)
            {
                hitBonus = Convert.ToInt32(bonus);
            }
        }

        return dexMod + hitBonus;
    }

    public static int CalculateArmorClass(Entity defender, string damageType)
    {
        var dexMod = defender.Stats.Dexterity - 10;
        var equipmentAC = 0;

        var acProp = defender.GetProperty<object>(CombatProperties.ArmorClass(damageType));
        if (acProp != null)
        {
            equipmentAC = Convert.ToInt32(acProp);
        }

        return BaseAC + equipmentAC + dexMod;
    }

    public static HitResult ResolveHit(Entity attacker, Entity defender, Entity? weapon, Random? random = null)
    {
        var damageType = GetWeaponDamageType(weapon);
        var naturalRoll = DiceRoller.RollD20(random);

        if (naturalRoll == 1)
        {
            var ac = CalculateArmorClass(defender, damageType);
            return new HitResult(false, false, true, naturalRoll, naturalRoll + CalculateHitRoll(attacker, weapon), ac);
        }

        if (naturalRoll == 20)
        {
            var ac = CalculateArmorClass(defender, damageType);
            return new HitResult(true, true, false, naturalRoll, naturalRoll + CalculateHitRoll(attacker, weapon), ac);
        }

        var hitroll = CalculateHitRoll(attacker, weapon);
        var totalRoll = naturalRoll + hitroll;
        var targetAC = CalculateArmorClass(defender, damageType);

        return new HitResult(totalRoll >= targetAC, false, false, naturalRoll, totalRoll, targetAC);
    }

    public static int CalculateDamage(Entity attacker, string damageDice, Random? random = null)
    {
        var baseDamage = DiceRoller.Roll(damageDice, random);
        var strMod = attacker.Stats.Strength - 10;
        var scaling = 1.0 + (strMod * 0.05);
        var scaledDamage = (int)(baseDamage * scaling);

        return Math.Max(1, scaledDamage);
    }

    public static string GetWeaponDamageDice(Entity? weapon)
    {
        if (weapon == null)
        {
            return UnarmedDamageDice;
        }

        var dice = weapon.GetProperty<string>(CombatProperties.DamageDice);
        return string.IsNullOrEmpty(dice) ? UnarmedDamageDice : dice;
    }

    public static Entity? GetWieldedWeapon(Entity entity)
    {
        return entity.GetEquipment("wield");
    }
}
