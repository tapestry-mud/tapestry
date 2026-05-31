using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Data;
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
    public void RecommendBroker_resolves_but_binds_nothing_by_default()
    {
        // Default config has llm.enabled:false and llm.use_stub:false, so the broker binds
        // no provider. The stub is no longer the unconditional prod fallback (see plan Task 13);
        // it's a test fixture + opt-in use_stub escape hatch.
        using var provider = BuildProvider();
        var broker = provider.GetRequiredService<RecommendBroker>();
        Assert.NotNull(broker);
        Assert.False(broker.HasProvider);
        Assert.False(broker.IsEnabled);
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
        //
        // CreateRoom writes a room side-car to the configured RoomsPath. We point that at
        // an absolute temp dir (registered BEFORE AddTapestryEngine, whose TryAddSingleton
        // then yields to ours) so the write lands under our temp root and cleanup removes
        // it -- otherwise the default "./data/areas" leaks a stray file into the test dir.
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "authdi-" + System.IO.Path.GetRandomFileName());
        var cfg = new ServerConfig();
        cfg.Persistence.RoomsPath = root; // absolute -> ResolveDataPath returns it verbatim

        var services = new ServiceCollection();
        services.AddSingleton(cfg);
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        using var provider = services.BuildServiceProvider();

        var holder = provider.GetRequiredService<LoadedPackNamespaces>();
        var module = provider.GetRequiredService<WorldAuthoringModule>();

        // Populate via the holder (as PackLoader does), then assert the module sees it
        // by exercising the namespace gate: createRoom rejects unknown namespaces and
        // accepts a namespace added to the shared set after the module was constructed.
        Assert.False(module.CreateRoom("x", "late-pack:room", "Room", "desc"));
        holder.Add("late-pack");
        var world = provider.GetRequiredService<World>();
        try
        {
            // The namespace gate and world mutation happen before the side-car write.
            // The gate passing is proven by the room landing in the world; the write
            // now lands under our temp root rather than polluting the test working dir.
            module.CreateRoom("late-area", "late-pack:room", "Room", "desc");
            Assert.NotNull(world.GetRoom("late-pack:room"));
            Assert.True(System.IO.Directory.Exists(System.IO.Path.Combine(root, "late-area")));
        }
        finally
        {
            if (System.IO.Directory.Exists(root)) { System.IO.Directory.Delete(root, recursive: true); }
        }
    }
}
