// tests/Tapestry.Engine.Tests/Combat/HitResolverTests.cs
using Tapestry.Engine.Combat;

namespace Tapestry.Engine.Tests.Combat;

public class HitResolverTests
{
    private Entity CreateAttacker(int dex = 10)
    {
        var entity = new Entity("player", "TestAttacker");
        entity.Stats.BaseDexterity = dex;
        entity.Stats.BaseStrength = 10;
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.Hp = 100;
        return entity;
    }

    private Entity CreateDefender(int dex = 10)
    {
        var entity = new Entity("npc", "TestDefender");
        entity.Stats.BaseDexterity = dex;
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.Hp = 100;
        entity.SetProperty("ac_slash", 0);
        entity.SetProperty("ac_pierce", 0);
        entity.SetProperty("ac_bash", 0);
        entity.SetProperty("ac_exotic", 0);
        return entity;
    }

    private Entity CreateWeapon(string damageDice = "1d6+0", int hitBonus = 0, string damageType = "slash")
    {
        var weapon = new Entity("item:weapon", "a test sword");
        weapon.SetProperty("damage_dice", damageDice);
        weapon.SetProperty("hit_bonus", hitBonus);
        weapon.SetProperty("damage_type", damageType);
        return weapon;
    }

    [Fact]
    public void CalculateHitRoll_BaseCase_DexModifiesRoll()
    {
        var attacker = CreateAttacker(dex: 14);
        var weapon = CreateWeapon(hitBonus: 2);
        // hitroll = base(0) + hit_bonus(2) + dex_mod(14-10=4) = 6
        var hitroll = HitResolver.CalculateHitRoll(attacker, weapon);
        Assert.Equal(6, hitroll);
    }

    [Fact]
    public void CalculateHitRoll_NoWeapon_ReturnsBaseDexMod()
    {
        var attacker = CreateAttacker(dex: 12);
        var hitroll = HitResolver.CalculateHitRoll(attacker, null);
        Assert.Equal(2, hitroll); // dex_mod only: 12-10=2
    }

    [Fact]
    public void CalculateArmorClass_SlashType()
    {
        var defender = CreateDefender(dex: 14);
        defender.SetProperty("ac_slash", 5);
        // AC = base(10) + ac_slash(5) + dex_mod(14-10=4) = 19
        var ac = HitResolver.CalculateArmorClass(defender, "slash");
        Assert.Equal(19, ac);
    }

    [Fact]
    public void CalculateArmorClass_DifferentTypesReturnDifferentValues()
    {
        var defender = CreateDefender(dex: 10);
        defender.SetProperty("ac_slash", 5);
        defender.SetProperty("ac_pierce", 2);
        defender.SetProperty("ac_bash", 8);
        defender.SetProperty("ac_exotic", 0);

        Assert.Equal(15, HitResolver.CalculateArmorClass(defender, "slash"));
        Assert.Equal(12, HitResolver.CalculateArmorClass(defender, "pierce"));
        Assert.Equal(18, HitResolver.CalculateArmorClass(defender, "bash"));
        Assert.Equal(10, HitResolver.CalculateArmorClass(defender, "exotic"));
    }

    [Fact]
    public void CalculateArmorClass_MissingTypeProperty_UsesBaseOnly()
    {
        var defender = CreateDefender(dex: 10);
        // ac_slash etc. are set to 0 by CreateDefender, but ac_exotic is not set on a fresh entity
        var freshDefender = new Entity("npc", "Fresh");
        freshDefender.Stats.BaseDexterity = 10;
        // No ac_ properties set at all
        var ac = HitResolver.CalculateArmorClass(freshDefender, "slash");
        Assert.Equal(10, ac);
    }

    [Fact]
    public void ResolveHit_Natural20_AlwaysHits()
    {
        var attacker = CreateAttacker(dex: 1);
        var defender = CreateDefender(dex: 20);
        defender.SetProperty("ac_bash", 50);
        var random = new FixedRandom(20);
        // null weapon = unarmed = bash
        var result = HitResolver.ResolveHit(attacker, defender, null, random);
        Assert.True(result.IsHit);
        Assert.True(result.IsCritical);
    }

    [Fact]
    public void ResolveHit_Natural1_AlwaysMisses()
    {
        var attacker = CreateAttacker(dex: 20);
        var defender = CreateDefender(dex: 1);
        defender.SetProperty("ac_bash", -100);
        var random = new FixedRandom(1);
        var result = HitResolver.ResolveHit(attacker, defender, null, random);
        Assert.False(result.IsHit);
        Assert.True(result.IsFumble);
    }

    [Fact]
    public void ResolveHit_RollMeetsAC_Hits()
    {
        var attacker = CreateAttacker(dex: 10);
        var defender = CreateDefender(dex: 10);
        // All ac_ types default to 0 from CreateDefender, AC = 10
        var random = new FixedRandom(10); // Roll 10 + hitroll 0 = 10 >= AC 10
        var result = HitResolver.ResolveHit(attacker, defender, null, random);
        Assert.True(result.IsHit);
    }

    [Fact]
    public void ResolveHit_RollBelowAC_Misses()
    {
        var attacker = CreateAttacker(dex: 10);
        var defender = CreateDefender(dex: 10);
        // All ac_ types default to 0 from CreateDefender, AC = 10
        var random = new FixedRandom(9); // Roll 9 + hitroll 0 = 9 < AC 10
        var result = HitResolver.ResolveHit(attacker, defender, null, random);
        Assert.False(result.IsHit);
    }

    [Fact]
    public void ResolveHit_UsesWeaponDamageTypeForAC()
    {
        var attacker = CreateAttacker(dex: 10);
        var defender = CreateDefender(dex: 10);
        defender.SetProperty("ac_slash", 5);  // AC vs slash = 15
        defender.SetProperty("ac_pierce", 0); // AC vs pierce = 10

        var slashWeapon = CreateWeapon(damageType: "slash");
        var pierceWeapon = CreateWeapon(damageType: "pierce");

        // Roll 12: hits AC 10 (pierce) but misses AC 15 (slash)
        var random = new FixedRandom(12);
        var slashResult = HitResolver.ResolveHit(attacker, defender, slashWeapon, random);
        var pierceResult = HitResolver.ResolveHit(attacker, defender, pierceWeapon, random);

        Assert.False(slashResult.IsHit);
        Assert.True(pierceResult.IsHit);
    }

    [Fact]
    public void CalculateDamage_AppliesStrScaling()
    {
        var attacker = CreateAttacker();
        attacker.Stats.BaseStrength = 14; // +20%
        // 1d1+10 = 11 base, 11 * 1.2 = 13.2 → 13
        var random = new FixedRandom(1);
        var damage = HitResolver.CalculateDamage(attacker, "1d1+10", random);
        Assert.Equal(13, damage);
    }

    [Fact]
    public void CalculateDamage_LowStr_ReducesDamage()
    {
        var attacker = CreateAttacker();
        attacker.Stats.BaseStrength = 6; // -20%
        // 1d1+10 = 11 base, 11 * 0.8 = 8.8 → 8
        var random = new FixedRandom(1);
        var damage = HitResolver.CalculateDamage(attacker, "1d1+10", random);
        Assert.Equal(8, damage);
    }

    [Fact]
    public void CalculateDamage_MinimumOneDamage()
    {
        var attacker = CreateAttacker();
        attacker.Stats.BaseStrength = 1;
        var random = new FixedRandom(1);
        var damage = HitResolver.CalculateDamage(attacker, "1d1-5", random);
        Assert.Equal(1, damage);
    }

    [Fact]
    public void GetWeaponDamageType_WithWeapon_ReturnsType()
    {
        var weapon = CreateWeapon(damageType: "pierce");
        Assert.Equal("pierce", HitResolver.GetWeaponDamageType(weapon));
    }

    [Fact]
    public void GetWeaponDamageType_NoWeapon_ReturnsBash()
    {
        Assert.Equal("bash", HitResolver.GetWeaponDamageType(null));
    }

    [Fact]
    public void GetWeaponDamageType_MissingProperty_ReturnsBash()
    {
        var weapon = new Entity("item:weapon", "bare knuckles");
        Assert.Equal("bash", HitResolver.GetWeaponDamageType(weapon));
    }

    [Fact]
    public void CalculateDamage_Unarmed()
    {
        var attacker = CreateAttacker();
        attacker.Stats.BaseStrength = 10;
        var random = new FixedRandom(1);
        var damage = HitResolver.CalculateDamage(attacker, HitResolver.UnarmedDamageDice, random);
        Assert.InRange(damage, 1, 2);
    }
}

/// <summary>
/// Test helper: always returns the same d20 roll.
/// </summary>
public class FixedRandom : Random
{
    private readonly int _fixedValue;
    public FixedRandom(int fixedValue) { _fixedValue = fixedValue; }
    public override int Next(int minValue, int maxValue)
    {
        return Math.Clamp(_fixedValue, minValue, maxValue - 1);
    }
}
