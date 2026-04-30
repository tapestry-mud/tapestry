using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Training;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class TrainingModuleTests
{
    private (JintRuntime rt, TrainingManager training, ProficiencyManager proficiency, World world)
        BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt,
            provider.GetRequiredService<TrainingManager>(),
            provider.GetRequiredService<ProficiencyManager>(),
            provider.GetRequiredService<World>());
    }

    private Entity CreatePlayer(World world)
    {
        var e = new Entity("player", "Tester");
        world.TrackEntity(e);
        return e;
    }

    [Fact]
    public void GetTrainsAvailable_RoundTrips()
    {
        var (rt, training, _, world) = BuildRuntime();
        var player = CreatePlayer(world);
        training.GrantTrains(player.Id, 7);

        var result = rt.Evaluate($"tapestry.training.getTrainsAvailable('{player.Id}')");
        Assert.Equal(7, Convert.ToInt32(result));
    }

    [Fact]
    public void GrantTrains_RoundTrips()
    {
        var (rt, training, _, world) = BuildRuntime();
        var player = CreatePlayer(world);

        rt.Execute($"tapestry.training.grantTrains('{player.Id}', 3)");
        Assert.Equal(3, training.GetTrainsAvailable(player.Id));
    }

    [Fact]
    public void GetCap_RoundTrips()
    {
        var (rt, _, proficiency, world) = BuildRuntime();
        var player = CreatePlayer(world);
        proficiency.Learn(player.Id, "dodge");
        proficiency.SetCap(player.Id, "dodge", 50);

        var result = rt.Evaluate($"tapestry.training.getCap('{player.Id}', 'dodge')");
        Assert.Equal("apprentice", result?.ToString());
    }

    [Fact]
    public void SetCap_RoundTrips()
    {
        var (rt, _, proficiency, world) = BuildRuntime();
        var player = CreatePlayer(world);
        proficiency.Learn(player.Id, "dodge");

        rt.Execute($"tapestry.training.setCap('{player.Id}', 'dodge', 'journeyman')");
        Assert.Equal(75, proficiency.GetCap(player.Id, "dodge"));
    }
}
