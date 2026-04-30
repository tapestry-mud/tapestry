// tests/Tapestry.Engine.Tests/Combat/DiceRollerTests.cs
using Tapestry.Engine.Combat;

namespace Tapestry.Engine.Tests.Combat;

public class DiceRollerTests
{
    [Fact]
    public void Roll_ParsesStandardNotation()
    {
        var result = DiceRoller.Roll("1d1+0");
        Assert.Equal(1, result);
    }

    [Fact]
    public void Roll_ParsesWithoutModifier()
    {
        var result = DiceRoller.Roll("1d1");
        Assert.Equal(1, result);
    }

    [Fact]
    public void Roll_AppliesPositiveModifier()
    {
        var result = DiceRoller.Roll("1d1+5");
        Assert.Equal(6, result);
    }

    [Fact]
    public void Roll_AppliesNegativeModifier()
    {
        var result = DiceRoller.Roll("1d1-1");
        Assert.Equal(0, result);
    }

    [Fact]
    public void Roll_MultipleDice()
    {
        var result = DiceRoller.Roll("3d1+0");
        Assert.Equal(3, result);
    }

    [Fact]
    public void Roll_WithRandomSeed_ReturnsInRange()
    {
        for (var i = 0; i < 100; i++)
        {
            var result = DiceRoller.Roll("2d6+3");
            Assert.InRange(result, 5, 15);
        }
    }

    [Fact]
    public void Roll_WithProvidedRandom_IsDeterministic()
    {
        var random = new Random(42);
        var result1 = DiceRoller.Roll("2d6+3", random);
        random = new Random(42);
        var result2 = DiceRoller.Roll("2d6+3", random);
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Roll_InvalidNotation_ReturnsZero()
    {
        Assert.Equal(0, DiceRoller.Roll("garbage"));
        Assert.Equal(0, DiceRoller.Roll(""));
    }

    [Fact]
    public void RollD20_ReturnsInRange()
    {
        for (var i = 0; i < 100; i++)
        {
            var result = DiceRoller.RollD20();
            Assert.InRange(result, 1, 20);
        }
    }

    [Fact]
    public void RollD20_WithRandom_IsDeterministic()
    {
        var random = new Random(42);
        var result1 = DiceRoller.RollD20(random);
        random = new Random(42);
        var result2 = DiceRoller.RollD20(random);
        Assert.Equal(result1, result2);
    }
}
