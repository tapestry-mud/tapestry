using Tapestry.Engine.Races;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Tests.Races;

public class RaceDefinitionTests
{
    [Fact]
    public void StatCaps_DefaultsToEmpty()
    {
        var def = new RaceDefinition { Id = "elf", Name = "Elf" };
        Assert.Empty(def.StatCaps);
    }

    [Fact]
    public void CastCostModifier_DefaultsToZero()
    {
        var def = new RaceDefinition { Id = "elf", Name = "Elf" };
        Assert.Equal(0, def.CastCostModifier);
    }

    [Fact]
    public void RacialFlags_DefaultsToEmpty()
    {
        var def = new RaceDefinition { Id = "elf", Name = "Elf" };
        Assert.Empty(def.RacialFlags);
    }

    [Fact]
    public void AllFieldsCanBeSet()
    {
        var def = new RaceDefinition
        {
            Id = "elf",
            Name = "Elf",
            StatCaps = new Dictionary<StatType, int>
            {
                { StatType.Strength, 25 },
                { StatType.MaxHp, 20 }
            },
            CastCostModifier = 40,
            RacialFlags = new List<string> { "resist_poison", "regen" }
        };
        Assert.Equal(25, def.StatCaps[StatType.Strength]);
        Assert.Equal(40, def.CastCostModifier);
        Assert.Contains("resist_poison", def.RacialFlags);
    }

    [Fact]
    public void Tagline_DefaultsToEmpty()
    {
        var def = new RaceDefinition { Id = "human", Name = "Human" };
        Assert.Equal("", def.Tagline);
    }

    [Fact]
    public void Description_DefaultsToEmpty()
    {
        var def = new RaceDefinition { Id = "human", Name = "Human" };
        Assert.Equal("", def.Description);
    }

    [Fact]
    public void RaceCategory_DefaultsToEmpty()
    {
        var def = new RaceDefinition { Id = "human", Name = "Human" };
        Assert.Equal("", def.RaceCategory);
    }

    [Fact]
    public void StartingAlignment_DefaultsToZero()
    {
        var def = new RaceDefinition { Id = "human", Name = "Human" };
        Assert.Equal(0, def.StartingAlignment);
    }
}
