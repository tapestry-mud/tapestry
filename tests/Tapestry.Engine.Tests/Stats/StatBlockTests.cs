// tests/Tapestry.Engine.Tests/Stats/StatBlockTests.cs
using FluentAssertions;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Tests.Stats;

public class StatBlockTests
{
    [Fact]
    public void NewStatBlock_DefaultsToZero()
    {
        var stats = new StatBlock();
        stats.BaseStrength.Should().Be(0);
        stats.Strength.Should().Be(0);
        stats.Hp.Should().Be(0);
        stats.MaxHp.Should().Be(0);
    }

    [Fact]
    public void SetBase_UpdatesEffective()
    {
        var stats = new StatBlock();
        stats.BaseStrength = 20;
        stats.Strength.Should().Be(20);
    }

    [Fact]
    public void AddModifier_UpdatesEffective()
    {
        var stats = new StatBlock();
        stats.BaseStrength = 20;
        stats.AddModifier(new StatModifier("equipment:gauntlets", StatType.Strength, 5));
        stats.Strength.Should().Be(25);
    }

    [Fact]
    public void RemoveModifiersBySource_UpdatesEffective()
    {
        var stats = new StatBlock();
        stats.BaseStrength = 20;
        stats.AddModifier(new StatModifier("equipment:gauntlets", StatType.Strength, 5));
        stats.AddModifier(new StatModifier("buff:rage", StatType.Strength, 10));
        stats.Strength.Should().Be(35);

        stats.RemoveModifiersBySource("equipment:gauntlets");
        stats.Strength.Should().Be(30);
    }

    [Fact]
    public void MultipleModifiers_SameStat_Stack()
    {
        var stats = new StatBlock();
        stats.BaseMaxHp = 100;
        stats.AddModifier(new StatModifier("equipment:helm", StatType.MaxHp, 50));
        stats.AddModifier(new StatModifier("equipment:armor", StatType.MaxHp, 100));
        stats.MaxHp.Should().Be(250);
    }

    [Fact]
    public void Vitals_CurrentClampedToMax()
    {
        var stats = new StatBlock();
        stats.BaseMaxHp = 100;
        stats.Hp = 150;
        stats.Hp.Should().Be(100);
    }

    [Fact]
    public void Vitals_CanSetCurrentDirectly()
    {
        var stats = new StatBlock();
        stats.BaseMaxHp = 100;
        stats.Hp = 100;
        stats.Hp = 50;
        stats.Hp.Should().Be(50);
    }

    [Fact]
    public void AllAttributes_WorkIndependently()
    {
        var stats = new StatBlock();
        stats.BaseStrength = 10;
        stats.BaseIntelligence = 15;
        stats.BaseWisdom = 12;
        stats.BaseDexterity = 18;
        stats.BaseConstitution = 14;
        stats.BaseLuck = 8;

        stats.AddModifier(new StatModifier("race:human", StatType.Dexterity, 3));

        stats.Strength.Should().Be(10);
        stats.Intelligence.Should().Be(15);
        stats.Wisdom.Should().Be(12);
        stats.Dexterity.Should().Be(21);
        stats.Constitution.Should().Be(14);
        stats.Luck.Should().Be(8);
    }

    [Fact]
    public void AllVitals_WorkIndependently()
    {
        var stats = new StatBlock();
        stats.BaseMaxHp = 100;
        stats.BaseMaxResource = 80;
        stats.BaseMaxMovement = 120;

        stats.Hp = 100;
        stats.Resource = 80;
        stats.Movement = 120;

        stats.AddModifier(new StatModifier("equipment:ring", StatType.MaxResource, 50));

        stats.MaxHp.Should().Be(100);
        stats.MaxResource.Should().Be(130);
        stats.MaxMovement.Should().Be(120);
        stats.Resource.Should().Be(80);  // current unchanged
    }

    [Fact]
    public void GetModifiers_ReturnsAll()
    {
        var stats = new StatBlock();
        stats.AddModifier(new StatModifier("a", StatType.Strength, 1));
        stats.AddModifier(new StatModifier("b", StatType.Intelligence, 2));
        stats.Modifiers.Should().HaveCount(2);
    }
}
