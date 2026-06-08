using FluentAssertions;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Text;

namespace Tapestry.Engine.Tests.Text;

public class OutputWidthServiceTests
{
    // --- pure overload (no entity) ---
    [Fact] public void Pref_Used() => OutputWidthService.Resolve(120, 80).Should().Be(120);
    [Fact] public void Pref_Zero_Off() => OutputWidthService.Resolve(0, 80).Should().Be(0);
    [Fact] public void Pref_Negative_Off() => OutputWidthService.Resolve(-3, 80).Should().Be(0);
    [Fact] public void Pref_ClampedMin() => OutputWidthService.Resolve(5, 80).Should().Be(OutputWidthService.MinWidth);
    [Fact] public void Pref_ClampedMax() => OutputWidthService.Resolve(99999, 80).Should().Be(OutputWidthService.MaxWidth);
    [Fact] public void NoPref_Default() => OutputWidthService.Resolve(null, 80).Should().Be(80);
    [Fact] public void NoPref_DefaultZero_Off() => OutputWidthService.Resolve(null, 0).Should().Be(0);

    // --- entity overload ---
    private static OutputWidthService Svc(int defaultWidth = 80)
    {
        var cfg = new ServerConfig();
        cfg.Output.WrapWidth = defaultWidth;
        return new OutputWidthService(cfg);
    }

    [Fact]
    public void NullEntity_UsesDefault() => Svc(80).Resolve(null).Should().Be(80);

    [Fact]
    public void EntityWithoutPref_UsesDefault()
    {
        var e = new Entity("player", "Tester");
        Svc(80).Resolve(e).Should().Be(80);
    }

    [Fact]
    public void EntityPref_UsedAndClamped()
    {
        var e = new Entity("player", "Tester");
        e.SetProperty(CommonProperties.ScreenWidth, 120);
        Svc(80).Resolve(e).Should().Be(120);

        e.SetProperty(CommonProperties.ScreenWidth, 0);
        Svc(80).Resolve(e).Should().Be(0);

        e.SetProperty(CommonProperties.ScreenWidth, 10);
        Svc(80).Resolve(e).Should().Be(OutputWidthService.MinWidth);
    }

    [Fact]
    public void Default_ReflectsConfig() => Svc(72).Default.Should().Be(72);
}
