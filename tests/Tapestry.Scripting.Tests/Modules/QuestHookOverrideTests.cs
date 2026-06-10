using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Registration;
using Tapestry.Scripting.Interop;
using Tapestry.Scripting.Modules;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class QuestHookOverrideTests
{
    private static (JintRuntime rt, RegistrationPolicy policy, QuestScriptLoader loader, World world)
        BuildRuntime(Dictionary<string, List<string>>? deps = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var graph = provider.GetRequiredService<PackDependencyGraph>();
        graph.Build(deps ?? new Dictionary<string, List<string>>());
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<RegistrationPolicy>(),
                provider.GetRequiredService<QuestScriptLoader>(),
                provider.GetRequiredService<World>());
    }

    private const string Intro =
        "tapestry.quests.registerScript('quest:intro', { onGranted: function(player){ return false; } });";
    private const string IntroB =
        "tapestry.quests.registerScript('quest:intro', { onGranted: function(player){ return true; } });";
    private const string IntroBOverride =
        "tapestry.quests.registerScript('quest:intro', { onGranted: function(player){ return true; }, override: true });";

    [Fact]
    public void RegisterScript_IsDeferred_UntilSeal()
    {
        var (rt, policy, loader, _) = BuildRuntime();
        rt.Execute(Intro, "pack-a", "scripts/a.js");
        loader.HasScript("quest:intro").Should().BeFalse("quest hooks must commit at the seal, not eagerly");
        policy.Resolve();
        loader.HasScript("quest:intro").Should().BeTrue();
    }

    [Fact]
    public void TwoPacks_SameQuest_NoOverride_BootError_NamingBothPacks()
    {
        var (rt, policy, _, _) = BuildRuntime();
        rt.Execute(Intro, "pack-a", "scripts/a.js");
        rt.Execute(IntroB, "pack-b", "scripts/b.js");
        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("quest:intro").And.Contain("override")
            .And.Contain("pack-a").And.Contain("pack-b");
    }

    [Fact]
    public void Override_WithDeclaredEdge_Wins()
    {
        var (rt, policy, loader, world) = BuildRuntime(new() { ["pack-b"] = new() { "pack-a" } });
        rt.Execute(Intro, "pack-a", "scripts/a.js");
        rt.Execute(IntroBOverride, "pack-b", "scripts/b.js");
        policy.Resolve();

        loader.HasScript("quest:intro").Should().BeTrue();

        // Winner identity by behavior: pack-a's onGranted returns false, pack-b's returns true.
        var player = new Entity("player", "Rand");
        world.TrackEntity(player);
        loader.CallOnGranted("quest:intro", player.Id).Should().BeTrue(
            "the override's hooks must win");
    }

    [Fact]
    public void Override_WithoutEdge_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        rt.Execute(Intro, "pack-a", "scripts/a.js");
        rt.Execute(IntroBOverride, "pack-b", "scripts/b.js");
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }

    [Fact]
    public void RegisterScript_ViaGlobPath_RecordsRealOwner()
    {
        var (rt, policy, loader, _) = BuildRuntime();
        rt.Execute(Intro, "pack-a", "scripts/quests/intro.js");
        policy.Resolve();

        loader.OwnerOf("quest:intro").Should().Be("pack-a",
            "hooks executed through the attributed glob path must bind under their real owning pack");
    }
}
