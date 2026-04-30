// tests/Tapestry.Engine.Tests/Combat/HealthTierTests.cs
using Tapestry.Engine.Combat;

namespace Tapestry.Engine.Tests.Combat;

public class HealthTierTests
{
    [Theory]
    [InlineData(100, 100, "perfect", "is in perfect health")]
    [InlineData(99, 100, "few scratches", "has a few scratches")]
    [InlineData(75, 100, "few scratches", "has a few scratches")]
    [InlineData(74, 100, "small wounds", "has some small wounds")]
    [InlineData(50, 100, "small wounds", "has some small wounds")]
    [InlineData(49, 100, "wounded", "is wounded")]
    [InlineData(35, 100, "wounded", "is wounded")]
    [InlineData(34, 100, "badly wounded", "is badly wounded")]
    [InlineData(20, 100, "badly wounded", "is badly wounded")]
    [InlineData(19, 100, "bleeding profusely", "is bleeding profusely")]
    [InlineData(10, 100, "bleeding profusely", "is bleeding profusely")]
    [InlineData(9, 100, "near death", "is near death")]
    [InlineData(1, 100, "near death", "is near death")]
    [InlineData(0, 100, "near death", "is near death")]
    public void GetTier_ReturnsExpected(int hp, int maxHp, string expectedTier, string expectedText)
    {
        var result = HealthTier.Get(hp, maxHp);
        Assert.Equal(expectedTier, result.Tier);
        Assert.Equal(expectedText, result.Text);
    }

    [Theory]
    [InlineData(-50, 100, "near death", "is near death")]
    [InlineData(150, 100, "perfect", "is in perfect health")]
    public void GetTier_OutOfRangeHp_ClampsToExpected(int hp, int maxHp, string expectedTier, string expectedText)
    {
        var result = HealthTier.Get(hp, maxHp);
        Assert.Equal(expectedTier, result.Tier);
        Assert.Equal(expectedText, result.Text);
    }

    [Fact]
    public void GetTier_ZeroMaxHp_ReturnsNearDeath()
    {
        var result = HealthTier.Get(0, 0);
        Assert.Equal("near death", result.Tier);
        Assert.Equal("is near death", result.Text);
    }
}
