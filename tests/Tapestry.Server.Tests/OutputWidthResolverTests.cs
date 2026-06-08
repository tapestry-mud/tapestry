using FluentAssertions;
using Tapestry.Server;

namespace Tapestry.Server.Tests;

public class OutputWidthResolverTests
{
    [Fact]
    public void PlayerPref_UsedWhenSet()
    {
        OutputWidthResolver.Resolve(playerPref: 120, defaultWidth: 80).Should().Be(120);
    }

    [Fact]
    public void PlayerPref_Zero_Disables()
    {
        OutputWidthResolver.Resolve(0, 80).Should().Be(0);
    }

    [Fact]
    public void PlayerPref_Negative_Disables()
    {
        OutputWidthResolver.Resolve(-5, 80).Should().Be(0);
    }

    [Fact]
    public void PlayerPref_ClampedToMax()
    {
        OutputWidthResolver.Resolve(99999, 80).Should().Be(OutputWidthResolver.MaxWidth);
    }

    [Fact]
    public void PlayerPref_ClampedToMin()
    {
        OutputWidthResolver.Resolve(5, 80).Should().Be(OutputWidthResolver.MinWidth);
    }

    [Fact]
    public void NoPref_UsesDefault()
    {
        OutputWidthResolver.Resolve(null, 80).Should().Be(80);
    }

    [Fact]
    public void NoPref_DefaultZero_Disables()
    {
        OutputWidthResolver.Resolve(null, 0).Should().Be(0);
    }

    [Fact]
    public void NoPref_TinyDefault_ClampedToMin()
    {
        OutputWidthResolver.Resolve(null, 10).Should().Be(OutputWidthResolver.MinWidth);
    }
}
