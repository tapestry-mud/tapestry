using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Recommend;
using Tapestry.Scripting;
using Tapestry.Scripting.Authoring;
using Tapestry.Scripting.Modules;
using Xunit;

namespace Tapestry.Scripting.Tests;

/// <summary>
/// DI smoke test for the authoring keystone wiring (Task 14). Builds the real
/// container via AddTapestryEngine + AddTapestryScripting and asserts the new
/// services resolve and the authoring module is discoverable as an IJintApiModule.
/// (Live strict-boot is a separate human-run task.)
/// </summary>
public class AuthoringDiResolutionTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void RecommendBroker_resolves_with_stub_bound()
    {
        using var provider = BuildProvider();
        var broker = provider.GetRequiredService<RecommendBroker>();
        Assert.NotNull(broker);
        Assert.True(broker.HasProvider);
    }

    [Fact]
    public void RoomProjector_resolves()
    {
        using var provider = BuildProvider();
        Assert.NotNull(provider.GetRequiredService<RoomProjector>());
    }

    [Fact]
    public void AuthoredRoomLoader_resolves()
    {
        using var provider = BuildProvider();
        Assert.NotNull(provider.GetRequiredService<AuthoredRoomLoader>());
    }

    [Fact]
    public void WorldAuthoringModule_resolves()
    {
        using var provider = BuildProvider();
        Assert.NotNull(provider.GetRequiredService<WorldAuthoringModule>());
    }

    [Fact]
    public void JintApiModules_includes_authoring_module()
    {
        using var provider = BuildProvider();
        var modules = provider.GetServices<IJintApiModule>().ToList();
        var authoring = modules.OfType<WorldAuthoringModule>().SingleOrDefault();
        Assert.NotNull(authoring);
        Assert.Equal("authoring", authoring!.Namespace);
    }

    [Fact]
    public void WorldAuthoringModule_shares_the_live_namespace_set()
    {
        // The module must hold the SAME HashSet the holder exposes, so namespaces
        // populated by PackLoader after construction are visible at a runtime createRoom.
        using var provider = BuildProvider();
        var holder = provider.GetRequiredService<LoadedPackNamespaces>();
        var module = provider.GetRequiredService<WorldAuthoringModule>();

        // Populate via the holder (as PackLoader does), then assert the module sees it
        // by exercising the namespace gate: createRoom rejects unknown namespaces and
        // accepts a namespace added to the shared set after the module was constructed.
        Assert.False(module.CreateRoom("x", "late-pack:room", "Room", "desc"));
        holder.Add("late-pack");
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "authdi-" + System.IO.Path.GetRandomFileName());
        var world = provider.GetRequiredService<World>();
        try
        {
            // CreateRoom writes a side-car to the configured rooms root; the default
            // ./data/areas may not be writable in all CI layouts, but the namespace gate
            // and world mutation happen before the write succeeds or throws. We assert the
            // gate passed by checking the room landed in the world.
            module.CreateRoom("late-area", "late-pack:room", "Room", "desc");
            Assert.NotNull(world.GetRoom("late-pack:room"));
        }
        finally
        {
            if (System.IO.Directory.Exists(root)) { System.IO.Directory.Delete(root, recursive: true); }
        }
    }
}
