using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class StatsModuleTests
{
    private (JintRuntime rt, World world) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<World>());
    }

    [Fact]
    public void RestoreVitals_SetsCurrentVitalsToMax()
    {
        var (rt, world) = BuildRuntime();
        var mob = new Entity("npc", "goblin guard");
        mob.AddTag("npc");
        world.TrackEntity(mob);
        mob.Stats.BaseMaxHp = 100;
        mob.Stats.BaseMaxResource = 80;
        mob.Stats.BaseMaxMovement = 120;
        mob.Stats.Invalidate();
        mob.Stats.Hp = 10;
        mob.Stats.Resource = 5;
        mob.Stats.Movement = 20;

        rt.Execute($"tapestry.stats.restoreVitals('{mob.Id}')");

        Assert.Equal(mob.Stats.MaxHp, mob.Stats.Hp);
        Assert.Equal(mob.Stats.MaxResource, mob.Stats.Resource);
        Assert.Equal(mob.Stats.MaxMovement, mob.Stats.Movement);
    }

    [Fact]
    public void RestoreVitals_InvalidId_DoesNotThrow()
    {
        var (rt, _) = BuildRuntime();
        var ex = Record.Exception(() => rt.Execute("tapestry.stats.restoreVitals('not-a-guid')"));
        Assert.Null(ex);
    }

    [Fact]
    public void RestoreVitals_UnknownGuid_DoesNotThrow()
    {
        var (rt, _) = BuildRuntime();
        var unknownId = Guid.NewGuid().ToString();
        var ex = Record.Exception(() => rt.Execute($"tapestry.stats.restoreVitals('{unknownId}')"));
        Assert.Null(ex);
    }
}
