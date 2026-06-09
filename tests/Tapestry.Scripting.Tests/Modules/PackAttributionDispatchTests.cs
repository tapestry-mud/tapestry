using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Registration;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

/// <summary>
/// Regression tests for __currentPack attribution at DEFERRED handler dispatch.
///
/// The interop API (tapestry.packs.call/has) attributes the CALLER pack by reading
/// the JS global __currentPack. That global is set per-script at load time. But
/// pack-registered handlers (commands, events, schedules, ...) run long after load,
/// when __currentPack holds whatever pack was loaded LAST — not the pack that
/// registered the handler. These tests drive the REAL dispatch path and assert the
/// live __currentPack equals the registering pack. The existing interop tests call
/// rt.Execute(script, packName) which sets __currentPack right before each call, so
/// they never exercised stale dispatch — that gap let the bug through.
/// </summary>
public class PackAttributionDispatchTests
{
    private (JintRuntime rt, CommandRegistry registry, World world, RegistrationPolicy policy) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<CommandRegistry>(), provider.GetRequiredService<World>(),
                provider.GetRequiredService<RegistrationPolicy>());
    }

    [Fact]
    public void CommandHandler_SeesRegisteringPack_NotLastLoadedPack()
    {
        var (rt, registry, world, policy) = BuildRuntime();

        // 1. Register a command in pack-a whose handler records the live __currentPack.
        rt.Execute(
            "tapestry.commands.register({ name:'probe', handler: function(p,a){ globalThis.__seenPack = __currentPack; } });",
            "pack-a");

        // 2. Load another script as pack-b AFTER, making the global __currentPack stale.
        rt.Execute("var x = 1;", "pack-b");

        // 3. Seal the registration ledger, then dispatch the command through the REAL registry path.
        policy.Resolve();
        var player = new Entity("player", "Tester");
        world.TrackEntity(player);
        var reg = registry.Resolve("probe");
        Assert.NotNull(reg);
        reg!.ActorHandler(new ActorContext
        {
            EntityId = player.Id,
            Name = player.Name,
            Source = "player",
            Command = "probe",
            RawArgs = []
        });

        // 4. The handler must have run attributed to pack-a, not the stale pack-b.
        Assert.Equal("pack-a", rt.Evaluate("globalThis.__seenPack"));
    }
}
