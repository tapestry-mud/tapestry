using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Quests;
using Tapestry.Scripting;
using Tapestry.Scripting.Modules;

namespace Tapestry.Scripting.Tests.Modules;

public class QuestScriptLoaderTests
{
    private (JintRuntime rt, World world, QuestScriptLoader loader) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<World>(), provider.GetRequiredService<QuestScriptLoader>());
    }

    [Fact]
    public void RegisterScript_StoresHooks_ViaJsApi()
    {
        var (rt, _, loader) = BuildRuntime();

        rt.Execute("tapestry.quests.registerScript('test:my-quest', { onGranted: function(player) { return true; } })");

        loader.HasScript("test:my-quest").Should().BeTrue();
    }

    [Fact]
    public void CallOnGranted_ReturnsFalse_WhenNoHookRegistered()
    {
        var (_, _, loader) = BuildRuntime();

        var result = loader.CallOnGranted("test:no-script", Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public void CallOnGranted_ReturnsTrue_WhenHookReturnsTrue()
    {
        var (rt, world, loader) = BuildRuntime();

        var player = new Entity("player", "Rand");
        world.TrackEntity(player);

        rt.Execute("tapestry.quests.registerScript('test:grant-true', { onGranted: function(player) { return true; } })");

        var result = loader.CallOnGranted("test:grant-true", player.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public void CallOnGranted_ReturnsFalse_WhenHookReturnsFalse()
    {
        var (rt, world, loader) = BuildRuntime();

        var player = new Entity("player", "Mat");
        world.TrackEntity(player);

        rt.Execute("tapestry.quests.registerScript('test:grant-false', { onGranted: function(player) { return false; } })");

        var result = loader.CallOnGranted("test:grant-false", player.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public void CallOnGranted_ReturnsFalse_WhenNoOnGrantedHookPresent()
    {
        var (rt, world, loader) = BuildRuntime();

        var player = new Entity("player", "Perrin");
        world.TrackEntity(player);

        rt.Execute("tapestry.quests.registerScript('test:no-granted', { onCompleted: function(player) {} })");

        var result = loader.CallOnGranted("test:no-granted", player.Id);

        result.Should().BeFalse();
    }
}
