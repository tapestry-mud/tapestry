using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Alignment;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class AlignmentModuleTests
{
    private (JintRuntime rt, AlignmentManager mgr, World world) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<AlignmentManager>(), provider.GetRequiredService<World>());
    }

    [Fact]
    public void Get_ReturnsStoredAlignment()
    {
        var (rt, mgr, world) = BuildRuntime();
        var entity = new Entity("player", "Tester");
        world.TrackEntity(entity);
        mgr.Set(entity.Id, 250, "test");
        var result = rt.Evaluate($"tapestry.alignment.get('{entity.Id}')");
        Assert.Equal(250, Convert.ToInt32(result));
    }

    [Fact]
    public void Bucket_ReturnsCorrectBucket()
    {
        var (rt, mgr, world) = BuildRuntime();
        var entity = new Entity("player", "Tester");
        world.TrackEntity(entity);
        mgr.Set(entity.Id, -400, "test");
        var result = rt.Evaluate($"tapestry.alignment.bucket('{entity.Id}')");
        Assert.Equal("evil", result?.ToString());
    }

    [Fact]
    public void Set_UpdatesAlignmentValue()
    {
        var (rt, _, world) = BuildRuntime();
        var entity = new Entity("player", "Tester");
        world.TrackEntity(entity);
        rt.Execute($"tapestry.alignment.set('{entity.Id}', 500, 'test');");
        Assert.Equal(500, entity.GetProperty<int?>("alignment"));
    }

    [Fact]
    public void Shift_AddsToAlignmentValue()
    {
        var (rt, mgr, world) = BuildRuntime();
        var entity = new Entity("player", "Tester");
        world.TrackEntity(entity);
        mgr.Set(entity.Id, 100, "init");
        rt.Execute($"tapestry.alignment.shift('{entity.Id}', 50, 'test');");
        Assert.Equal(150, mgr.Get(entity.Id));
    }

    [Fact]
    public void Configure_OverridesThresholds()
    {
        var (rt, mgr, world) = BuildRuntime();
        rt.Execute("tapestry.alignment.configure({ thresholds: { evil: -500, good: 500 } });");
        var entity = new Entity("player", "Tester");
        world.TrackEntity(entity);
        mgr.Set(entity.Id, -400, "test");
        // -400 is neutral with new thresholds (evil threshold is now -500)
        Assert.Equal("neutral", mgr.Bucket(entity.Id));
    }

    [Fact]
    public void SetGender_StoresGenderProperty()
    {
        var (rt, _, world) = BuildRuntime();
        var entity = new Entity("player", "Tester");
        world.TrackEntity(entity);
        rt.Execute($"tapestry.alignment.setGender('{entity.Id}', 'male');");
        Assert.Equal("male", entity.GetProperty<string>("gender"));
    }

    [Fact]
    public void GetGender_ReturnsStoredGender()
    {
        var (rt, _, world) = BuildRuntime();
        var entity = new Entity("player", "Tester");
        world.TrackEntity(entity);
        entity.SetProperty("gender", "female");
        var result = rt.Evaluate($"tapestry.alignment.getGender('{entity.Id}')");
        Assert.Equal("female", result?.ToString());
    }
}
