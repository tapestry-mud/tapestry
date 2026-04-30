// tests/Tapestry.Engine.Tests/Alignment/AlignmentRangeTests.cs
using Tapestry.Engine.Alignment;

namespace Tapestry.Engine.Tests.Alignment;

public class AlignmentRangeTests
{
    [Theory]
    [InlineData(-1000, true)]
    [InlineData(-350, true)]
    [InlineData(-349, false)]
    [InlineData(0, false)]
    public void MaxOnly_AllowsAtOrBelow(int alignment, bool expected)
    {
        var range = new AlignmentRange { Max = -350 };
        Assert.Equal(expected, range.Allows(alignment));
    }

    [Theory]
    [InlineData(350, true)]
    [InlineData(1000, true)]
    [InlineData(349, false)]
    [InlineData(0, false)]
    public void MinOnly_AllowsAtOrAbove(int alignment, bool expected)
    {
        var range = new AlignmentRange { Min = 350 };
        Assert.Equal(expected, range.Allows(alignment));
    }

    [Theory]
    [InlineData(-349, true)]
    [InlineData(0, true)]
    [InlineData(349, true)]
    [InlineData(-350, false)]
    [InlineData(350, false)]
    public void BothBounds_NeutralRange(int alignment, bool expected)
    {
        var range = new AlignmentRange { Min = -349, Max = 349 };
        Assert.Equal(expected, range.Allows(alignment));
    }

    [Fact]
    public void NoBounds_AlwaysAllows()
    {
        var range = new AlignmentRange();
        Assert.True(range.Allows(-1000));
        Assert.True(range.Allows(0));
        Assert.True(range.Allows(1000));
    }
}
