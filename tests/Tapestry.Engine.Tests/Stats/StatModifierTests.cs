using FluentAssertions;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Tests.Stats;

public class StatModifierTests
{
    [Fact]
    public void Modifier_StoresSourceStatAndValue()
    {
        var mod = new StatModifier("equipment:iron-helm", StatType.Strength, 5);
        mod.Source.Should().Be("equipment:iron-helm");
        mod.Stat.Should().Be(StatType.Strength);
        mod.Value.Should().Be(5);
    }

    [Fact]
    public void Modifier_DefaultsToFlat()
    {
        var mod = new StatModifier("buff:rage", StatType.MaxHp, 100);
        mod.ModifierType.Should().Be(ModifierType.Flat);
    }
}
