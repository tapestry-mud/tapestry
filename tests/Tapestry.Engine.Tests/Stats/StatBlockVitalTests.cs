using FluentAssertions;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Tests.Stats;

public class StatBlockVitalTests
{
    [Fact]
    public void SetVital_ClampsToMax_AndReturnsClampedValue()
    {
        var stats = new StatBlock { BaseMaxHp = 100 };
        stats.InitializeVitals(50, 0, 0);

        var applied = stats.SetVital(VitalKind.Hp, 9999);

        applied.Should().Be(100);
        stats.Hp.Should().Be(100);
    }

    [Fact]
    public void SetVital_ClampsToZeroFloor()
    {
        var stats = new StatBlock { BaseMaxHp = 100 };
        stats.InitializeVitals(50, 0, 0);

        var applied = stats.SetVital(VitalKind.Hp, -20);

        applied.Should().Be(0);
        stats.Hp.Should().Be(0);
    }

    [Fact]
    public void InitializeVitals_SetsAllThreeClamped()
    {
        var stats = new StatBlock { BaseMaxHp = 100, BaseMaxResource = 50, BaseMaxMovement = 80 };

        stats.InitializeVitals(120, 30, 200);

        stats.Hp.Should().Be(100);
        stats.Resource.Should().Be(30);
        stats.Movement.Should().Be(80);
    }
}
