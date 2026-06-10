using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Quests;
using Tapestry.Engine.Registration;
using Tapestry.Scripting;
using Tapestry.Scripting.Modules;

namespace Tapestry.Scripting.Tests.Modules;

public class QuestScriptLoaderTests
{
    // registerScript defers the loader write to the RegistrationPolicy seal barrier;
    // tests that register via JS call policy.Resolve() before reading back.
    private (JintRuntime rt, World world, QuestScriptLoader loader, RegistrationPolicy policy) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<World>(), provider.GetRequiredService<QuestScriptLoader>(),
                provider.GetRequiredService<RegistrationPolicy>());
    }

    [Fact]
    public void RegisterScript_StoresHooks_ViaJsApi()
    {
        var (rt, _, loader, policy) = BuildRuntime();

        rt.Execute("tapestry.quests.registerScript('test:my-quest', { onGranted: function(player) { return true; } })");
        policy.Resolve(); // quest hooks commit at the seal barrier

        loader.HasScript("test:my-quest").Should().BeTrue();
    }

    [Fact]
    public void CallOnGranted_ReturnsFalse_WhenNoHookRegistered()
    {
        var (_, _, loader, _) = BuildRuntime();

        var result = loader.CallOnGranted("test:no-script", Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public void CallOnGranted_ReturnsTrue_WhenHookReturnsTrue()
    {
        var (rt, world, loader, policy) = BuildRuntime();

        var player = new Entity("player", "Rand");
        world.TrackEntity(player);

        rt.Execute("tapestry.quests.registerScript('test:grant-true', { onGranted: function(player) { return true; } })");
        policy.Resolve(); // quest hooks commit at the seal barrier

        var result = loader.CallOnGranted("test:grant-true", player.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public void CallOnGranted_ReturnsFalse_WhenHookReturnsFalse()
    {
        var (rt, world, loader, policy) = BuildRuntime();

        var player = new Entity("player", "Mat");
        world.TrackEntity(player);

        rt.Execute("tapestry.quests.registerScript('test:grant-false', { onGranted: function(player) { return false; } })");
        policy.Resolve(); // quest hooks commit at the seal barrier

        var result = loader.CallOnGranted("test:grant-false", player.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public void CallOnGranted_ReturnsFalse_WhenNoOnGrantedHookPresent()
    {
        var (rt, world, loader, policy) = BuildRuntime();

        var player = new Entity("player", "Perrin");
        world.TrackEntity(player);

        rt.Execute("tapestry.quests.registerScript('test:no-granted', { onCompleted: function(player) {} })");
        policy.Resolve(); // quest hooks commit at the seal barrier

        var result = loader.CallOnGranted("test:no-granted", player.Id);

        result.Should().BeFalse();
    }
}
