using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Flow;
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

    [Fact]
    public void World_Factory_StillWiresThePlayerCreatorSingleton()
    {
        // World now resolves via a factory lambda (services.AddSingleton<World>(sp => ...))
        // instead of a plain services.AddSingleton<World>(). This guards the risk flagged
        // when that flip landed: PlayerCreator is an optional ctor parameter on World, and
        // the factory must still hand World the SAME PlayerCreator singleton the container
        // gives everyone else, not null and not a second instance.
        var services = new ServiceCollection();
        services.AddTapestryEngine();
        var provider = services.BuildServiceProvider();

        var world = provider.GetRequiredService<World>();
        var creator = provider.GetRequiredService<PlayerCreator>();

        var pending = new Entity("player", "PendingPlayer");
        creator.TrackEntity(pending);

        // If World's factory had failed to resolve PlayerCreator (null, or a different
        // instance), this fallback lookup would miss.
        Assert.Equal(pending, world.GetEntity(pending.Id));
    }

    [Fact]
    public void World_Factory_ResolvesTheStatusBroadcasterAsThePropertyObserver()
    {
        var services = new ServiceCollection();
        services.AddTapestryEngine();
        var provider = services.BuildServiceProvider();

        var observer = provider.GetService<IPropertyObserver>();
        Assert.IsType<EntityStatusBroadcaster>(observer);
    }
}
