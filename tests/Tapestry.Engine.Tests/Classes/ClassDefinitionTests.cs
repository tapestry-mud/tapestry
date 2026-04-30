using Tapestry.Engine.Classes;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Tests.Classes;

public class ClassDefinitionTests
{
    [Fact]
    public void StatGrowth_DefaultsToEmpty()
    {
        var def = new ClassDefinition { Id = "warrior", Name = "Warrior" };
        Assert.Empty(def.StatGrowth);
    }

    [Fact]
    public void PackName_DefaultsToEmpty()
    {
        var def = new ClassDefinition { Id = "warrior", Name = "Warrior" };
        Assert.Equal("", def.PackName);
    }

    [Fact]
    public void StatGrowth_CanBeSet()
    {
        var def = new ClassDefinition
        {
            Id = "warrior",
            Name = "Warrior",
            StatGrowth = new Dictionary<StatType, string>
            {
                { StatType.MaxHp, "2d6+2" },
                { StatType.MaxMovement, "1d4" }
            }
        };
        Assert.Equal("2d6+2", def.StatGrowth[StatType.MaxHp]);
        Assert.Equal("1d4", def.StatGrowth[StatType.MaxMovement]);
    }

    [Fact]
    public void Tagline_DefaultsToEmpty()
    {
        var def = new ClassDefinition { Id = "warrior", Name = "Warrior" };
        Assert.Equal("", def.Tagline);
    }

    [Fact]
    public void Description_DefaultsToEmpty()
    {
        var def = new ClassDefinition { Id = "warrior", Name = "Warrior" };
        Assert.Equal("", def.Description);
    }

    [Fact]
    public void Track_DefaultsToEmpty()
    {
        var def = new ClassDefinition { Id = "warrior", Name = "Warrior" };
        Assert.Equal("", def.Track);
    }

    [Fact]
    public void StartingAlignment_DefaultsToZero()
    {
        var def = new ClassDefinition { Id = "warrior", Name = "Warrior" };
        Assert.Equal(0, def.StartingAlignment);
    }

    [Fact]
    public void LevelUpFlavor_DefaultsToEmpty()
    {
        var def = new ClassDefinition { Id = "warrior", Name = "Warrior" };
        Assert.Equal("", def.LevelUpFlavor);
    }

    [Fact]
    public void AllowedCategories_DefaultsToEmpty()
    {
        var def = new ClassDefinition { Id = "warrior", Name = "Warrior" };
        Assert.Empty(def.AllowedCategories);
    }

    [Fact]
    public void AllowedGenders_DefaultsToEmpty()
    {
        var def = new ClassDefinition { Id = "warrior", Name = "Warrior" };
        Assert.Empty(def.AllowedGenders);
    }

    [Fact]
    public void Path_DefaultsToEmpty()
    {
        var def = new ClassDefinition { Id = "warrior", Name = "Warrior" };
        Assert.Empty(def.Path);
    }

    [Fact]
    public void TrainsPerLevel_DefaultsToFive()
    {
        var def = new ClassDefinition { Id = "warrior", Name = "Warrior" };
        Assert.Equal(5, def.TrainsPerLevel);
    }

    [Fact]
    public void GrowthBonuses_DefaultsToEmptyDict()
    {
        var def = new ClassDefinition { Id = "warrior", Name = "Warrior" };
        Assert.Empty(def.GrowthBonuses);
    }

    [Fact]
    public void Path_CanBeSet_WithAutoGrantEntry()
    {
        var def = new ClassDefinition
        {
            Id = "warrior",
            Name = "Warrior",
            Path = new List<ClassPathEntry>
            {
                new ClassPathEntry(1, "dodge", null),
                new ClassPathEntry(25, "heron_wading", "quest")
            }
        };
        Assert.Equal(2, def.Path.Count);
        Assert.Equal(1, def.Path[0].Level);
        Assert.Equal("dodge", def.Path[0].AbilityId);
        Assert.Null(def.Path[0].UnlockedVia);
        Assert.Equal("quest", def.Path[1].UnlockedVia);
    }
}
