using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Heartbeat;
using Tapestry.Engine.Races;

namespace Tapestry.Engine.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddTapestryEngine_RegistersClassRegistry()
    {
        var services = new ServiceCollection();
        services.AddTapestryEngine();
        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ClassRegistry>());
    }

    [Fact]
    public void AddTapestryEngine_RegistersRaceRegistry()
    {
        var services = new ServiceCollection();
        services.AddTapestryEngine();
        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<RaceRegistry>());
    }

    [Fact]
    public void AddTapestryEngine_ClassRegistry_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddTapestryEngine();
        var provider = services.BuildServiceProvider();
        var a = provider.GetService<ClassRegistry>();
        var b = provider.GetService<ClassRegistry>();
        Assert.Same(a, b);
    }

    [Fact]
    public void Swell_Singletons_Resolve()
    {
        var services = new ServiceCollection();
        services.AddTapestryEngine();
        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<SwellClockManager>());
        Assert.NotNull(provider.GetService<WindowValidatorRegistry>());
        Assert.NotNull(provider.GetService<SwellClockPulse>());
    }
}
