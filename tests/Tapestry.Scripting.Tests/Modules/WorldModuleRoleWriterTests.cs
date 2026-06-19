using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Scripting;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

/// <summary>
/// Verifies the addRole/removeRole world writers exposed on the JS surface toggle
/// roles on a real Entity in the World -- the seam grant/revoke commands will use.
/// </summary>
public class WorldModuleRoleWriterTests
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
    public void AddRole_GrantsRoleOnEntity()
    {
        var (rt, world) = BuildRuntime();
        var player = new Entity("player", "Tester");
        world.TrackEntity(player);

        EsmTest.Load(rt, "test-pack", $"tapestry.world.addRole('{player.Id}', 'builder');");

        Assert.True(player.HasRole("builder"));
    }

    [Fact]
    public void RemoveRole_RevokesRoleOnEntity()
    {
        var (rt, world) = BuildRuntime();
        var player = new Entity("player", "Tester");
        player.AddRole("builder");
        world.TrackEntity(player);

        EsmTest.Load(rt, "test-pack", $"tapestry.world.removeRole('{player.Id}', 'builder');");

        Assert.False(player.HasRole("builder"));
    }
}
