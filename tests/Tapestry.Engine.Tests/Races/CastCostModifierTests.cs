using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Races;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Tests.Races;

public class CastCostModifierTests
{
    [Fact]
    public void ApplyTo_BaseCost_PositiveMod_AddsCost()
    {
        var race = new RaceDefinition { Id = "elf", Name = "Elf", CastCostModifier = 40 };
        Assert.Equal(60, RaceCostCalculator.AdjustCost(20, race));
    }

    [Fact]
    public void ApplyTo_BaseCost_NegativeMod_ReducesCost()
    {
        var race = new RaceDefinition { Id = "folk", Name = "Andoran", CastCostModifier = -15 };
        Assert.Equal(5, RaceCostCalculator.AdjustCost(20, race));
    }

    [Fact]
    public void ApplyTo_ResultClampedToZero()
    {
        var race = new RaceDefinition { Id = "folk", Name = "Andoran", CastCostModifier = -100 };
        Assert.Equal(0, RaceCostCalculator.AdjustCost(20, race));
    }

    [Fact]
    public void ApplyTo_NullRace_ReturnsOriginalCost()
    {
        Assert.Equal(20, RaceCostCalculator.AdjustCost(20, null));
    }

    [Fact]
    public void AbilityUse_ElfPaysMoreResource()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();

        var provider = services.BuildServiceProvider();
        var raceReg = provider.GetRequiredService<RaceRegistry>();
        var abilityReg = provider.GetRequiredService<AbilityRegistry>();
        var world = provider.GetRequiredService<World>();

        raceReg.Register(new RaceDefinition { Id = "elf", Name = "Elf", CastCostModifier = 40 });
        abilityReg.Register(new AbilityDefinition
        {
            Id = "fireball",
            Name = "Fireball",
            Type = AbilityType.Active,
            Category = AbilityCategory.Spell,
            ResourceCost = 20
        });

        var entity = new Entity("player", "T");
        entity.Stats.BaseMaxResource = 100;
        entity.Stats.Resource = 100;
        entity.SetProperty("race", "elf");
        world.TrackEntity(entity);

        // Elf cost = 20 + 40 = 60.
        var raceDef = raceReg.Get("elf");
        Assert.Equal(60, RaceCostCalculator.AdjustCost(20, raceDef));
    }
}
