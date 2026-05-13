using Tapestry.Engine.Abilities;

namespace Tapestry.Engine.Tests.Abilities;

public class AbilityPropertiesTests
{
    [Fact]
    public void QueuedActions_KeyExists()
    {
        Assert.Equal("queued_actions", AbilityProperties.QueuedActions);
    }

    [Fact]
    public void LastAbilityUsed_KeyExists()
    {
        Assert.Equal("last_ability_used", AbilityProperties.LastAbilityUsed);
    }

    [Fact]
    public void Proficiency_ConstIsMapKey()
    {
        Assert.Equal("proficiency", AbilityProperties.Proficiency);
    }

    [Fact]
    public void Cap_ConstIsMapKey()
    {
        Assert.Equal("cap", AbilityProperties.Cap);
    }
}
