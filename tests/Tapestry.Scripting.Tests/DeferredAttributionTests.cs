using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Scripting;
using Tapestry.Scripting.Modules;

namespace Tapestry.Scripting.Tests;

/// <summary>
/// Proves that a deferred handler (schedule.every) attributes to its REGISTERING pack
/// via GetActivePack() even after a second pack loads last.
///
/// The old PackScope.InvokeAsPack mechanism explicitly set __currentPack = registering pack
/// before each invocation. After J2 removes PackScope, attribution is purely lexical:
/// engine.Invoke(fn) sets the active module to fn's defining module, so GetActivePack()
/// inside the callback returns the registering pack's name regardless of what loaded last.
/// </summary>
public class DeferredAttributionTests
{
    [Fact]
    public void ScheduleEvery_DeferredHandler_AttributesToRegisteringPack_NotLastLoaded()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        var loop = provider.GetRequiredService<GameLoop>();
        var bus = provider.GetRequiredService<EventBus>();
        var policy = provider.GetRequiredService<Tapestry.Engine.Registration.RegistrationPolicy>();
        rt.Initialize();

        string? capturedActivePack = null;

        // Subscribe from C# - fires synchronously inside engine.Invoke (pack-a's fn), so
        // GetActivePack() reads the lexically active module: pack-a's module, not pack-b's.
        bus.Subscribe("attribution.probe", _ =>
        {
            capturedActivePack = rt.GetActivePack();
        });

        // pack-a registers a schedule.every handler that publishes the probe event.
        EsmTest.Load(rt, "@tapestry/pack-a", """
            tapestry.schedule.every(1, function() {
                tapestry.events.publish('attribution.probe', {});
            });
        """);

        // pack-b loads AFTER pack-a - it becomes the "last loaded" pack in any global sense.
        // Before J2 (InvokeAsPack era) the explicitly captured packName kept attribution correct.
        // After J2 (plain engine.Invoke) lexical attribution via GetActivePack() must do the same.
        EsmTest.Load(rt, "@tapestry/pack-b", """
            // pack-b does nothing with schedule; just establishes it as last-loaded
            var _marker = 'pack-b-loaded';
        """);

        policy.Resolve();

        loop.Tick();

        capturedActivePack.Should().NotBeNull("the handler must have fired");
        capturedActivePack.Should().Be(
            "@tapestry/pack-a",
            "the deferred handler must attribute to pack-a (its registering pack), not pack-b (the last-loaded pack)");
    }
}
