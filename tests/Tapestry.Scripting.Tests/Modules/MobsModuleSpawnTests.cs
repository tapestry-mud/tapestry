using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

/// <summary>Covers both spawnMob call forms via the JS binding.</summary>
public class MobsModuleSpawnTests
{
    private static (JintRuntime rt, World world, SpawnManager spawnManager) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<World>(), provider.GetRequiredService<SpawnManager>());
    }

    [Fact]
    public void SpawnMob_TwoArgPositionalForm_ReturnsNonNull()
    {
        var (rt, world, spawnManager) = BuildRuntime();
        spawnManager.RegisterTemplate(new MobTemplate { Id = "wolf", Name = "wolf" });
        world.AddRoom(new Room("test:r1", "Spawn Room", "A test room."));

        var result = EsmTest.Eval(rt, "tapestry.mobs.spawnMob('wolf', 'test:r1')");

        Assert.NotNull(result);
    }

    [Fact]
    public void SpawnMob_OptionsObjectForm_ReturnsNonNull()
    {
        var (rt, world, spawnManager) = BuildRuntime();
        spawnManager.RegisterTemplate(new MobTemplate { Id = "wolf", Name = "wolf" });
        world.AddRoom(new Room("test:r2", "Spawn Room", "A test room."));

        var result = EsmTest.Eval(rt, "tapestry.mobs.spawnMob({ template: 'wolf', roomId: 'test:r2' })");

        Assert.NotNull(result);
    }
}
