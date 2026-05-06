// tests/Tapestry.Engine.Tests/Abilities/AbilityDefinitionTests.cs
using Tapestry.Engine.Abilities;

namespace Tapestry.Engine.Tests.Abilities;

public class AbilityDefinitionTests
{
    [Fact]
    public void PulseDelay_DefaultsToZero()
    {
        var def = new AbilityDefinition { Id = "kick", Name = "Kick" };
        Assert.Equal(0, def.PulseDelay);
    }

    [Fact]
    public void InitiateOnly_DefaultsToFalse()
    {
        var def = new AbilityDefinition { Id = "kick", Name = "Kick" };
        Assert.False(def.InitiateOnly);
    }

    [Fact]
    public void MaxChance_DefaultsToHundred()
    {
        var def = new AbilityDefinition { Id = "dodge", Name = "Dodge" };
        Assert.Equal(100, def.MaxChance);
    }

    [Fact]
    public void EffectDefinition_DefaultsToNull()
    {
        var def = new AbilityDefinition { Id = "kick", Name = "Kick" };
        Assert.Null(def.Effect);
    }

    [Fact]
    public void EffectDefinition_CanBeSet()
    {
        var def = new AbilityDefinition
        {
            Id = "sanctuary",
            Name = "Sanctuary",
            Effect = new AbilityEffectDefinition
            {
                EffectId = "sanctuary",
                DurationPulses = 60,
                Flags = new List<string> { "sanctuary" }
            }
        };
        Assert.NotNull(def.Effect);
        Assert.Equal("sanctuary", def.Effect!.EffectId);
        Assert.Equal(60, def.Effect.DurationPulses);
    }

    [Fact]
    public void ShortName_DefaultsToNull()
    {
        var def = new AbilityDefinition { Id = "heron_wading_in_the_rushes", Name = "Heron Wading in the Rushes" };
        Assert.Null(def.ShortName);
    }

    [Fact]
    public void ShortName_CanBeSet()
    {
        var def = new AbilityDefinition
        {
            Id = "heron_wading_in_the_rushes",
            Name = "Heron Wading in the Rushes",
            ShortName = "Heron"
        };
        Assert.Equal("Heron", def.ShortName);
    }

    [Fact]
    public void SourceFile_DefaultsToEmpty()
    {
        var def = new AbilityDefinition { Id = "kick", Name = "Kick" };
        Assert.Equal("", def.SourceFile);
    }

    [Fact]
    public void FailureProficiencyGainMultiplier_DefaultsToPointTwoFive()
    {
        var def = new AbilityDefinition { Id = "kick", Name = "Kick" };
        Assert.Equal(0.25, def.FailureProficiencyGainMultiplier);
    }

    [Fact]
    public void Variance_DefaultsToHundred()
    {
        var def = new AbilityDefinition { Id = "kick", Name = "Kick" };
        Assert.Equal(100, def.Variance);
    }

    [Fact]
    public void GainStat_DefaultsToNull()
    {
        var def = new AbilityDefinition { Id = "kick", Name = "Kick" };
        Assert.Null(def.GainStat);
    }

    [Fact]
    public void GainStatScale_DefaultsToZero()
    {
        var def = new AbilityDefinition { Id = "kick", Name = "Kick" };
        Assert.Equal(0.0, def.GainStatScale);
    }

    [Fact]
    public void RequiresSlot_DefaultsToNull()
    {
        var def = new AbilityDefinition { Id = "kick", Name = "Kick" };
        Assert.Null(def.RequiresSlot);
    }

    [Fact]
    public void RequiresSlotTag_DefaultsToNull()
    {
        var def = new AbilityDefinition { Id = "kick", Name = "Kick" };
        Assert.Null(def.RequiresSlotTag);
    }
}
