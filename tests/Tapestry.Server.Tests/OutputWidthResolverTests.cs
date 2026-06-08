using FluentAssertions;
using Tapestry.Server;

namespace Tapestry.Server.Tests;

public class OutputWidthResolverTests
{
    [Fact]
    public void PlayerPref_UsedWhenSet()
    {
        OutputWidthResolver.Resolve(playerPref: 120, supportsNaws: false, nawsWidth: 0, defaultWidth: 80)
            .Should().Be(120);
    }

    [Fact]
    public void PlayerPref_Zero_Disables()
    {
        OutputWidthResolver.Resolve(0, false, 0, 80).Should().Be(0);
    }

    [Fact]
    public void PlayerPref_Negative_Disables()
    {
        OutputWidthResolver.Resolve(-5, false, 0, 80).Should().Be(0);
    }

    [Fact]
    public void PlayerPref_ClampedToMax()
    {
        OutputWidthResolver.Resolve(99999, false, 0, 80).Should().Be(OutputWidthResolver.MaxWidth);
    }

    [Fact]
    public void PlayerPref_ClampedToMin()
    {
        OutputWidthResolver.Resolve(5, false, 0, 80).Should().Be(OutputWidthResolver.MinWidth);
    }

    [Fact]
    public void NoPref_UsesDefault()
    {
        OutputWidthResolver.Resolve(null, false, 0, 80).Should().Be(80);
    }

    [Fact]
    public void NoPref_DefaultZero_Disables()
    {
        OutputWidthResolver.Resolve(null, false, 0, 0).Should().Be(0);
    }

    [Fact]
    public void Naws_NarrowerThanCap_Caps()
    {
        OutputWidthResolver.Resolve(null, true, 60, 80).Should().Be(60);
    }

    [Fact]
    public void Naws_WiderThanCap_Ignored()
    {
        OutputWidthResolver.Resolve(null, true, 200, 80).Should().Be(80);
    }

    [Fact]
    public void Naws_NotSupported_Ignored()
    {
        OutputWidthResolver.Resolve(null, false, 40, 80).Should().Be(80);
    }

    [Fact]
    public void Naws_TooSmall_Ignored()
    {
        OutputWidthResolver.Resolve(null, true, 10, 80).Should().Be(80);
    }

    [Fact]
    public void PlayerPref_AndNawsNarrower_TakesNarrower()
    {
        OutputWidthResolver.Resolve(120, true, 70, 80).Should().Be(70);
    }
}
