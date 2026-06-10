using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Registration;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class MobCommandOverrideTests
{
    private static (JintRuntime rt, RegistrationPolicy policy, CommandRegistry reg, PackDependencyGraph graph)
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
                provider.GetRequiredService<CommandRegistry>(), graph);
    }

    private const string MobSay =
        "tapestry.mobs.registerCommand('say', { handler: function(mob, text) {} });";

    [Fact]
    public void MobCommand_IsDeferred_UntilSeal()
    {
        var (rt, policy, reg, _) = BuildRuntime();
        rt.Execute(MobSay, "tapestry-core", "scripts/mobs/commands.js");
        reg.Resolve("say", "mob").Should().BeNull();   // not committed yet
        policy.Resolve();
        reg.Resolve("say", "mob").Should().NotBeNull();
    }

    [Fact]
    public void PlayerVerb_SurvivesMobRegistration_98Regression()
    {
        var (rt, policy, reg, _) = BuildRuntime();
        // Same pack registers the player verb (Kind "command") and the mob verb
        // (Kind "mob-command") -- the exact @tapestry/core shape that killed say in prod.
        rt.Execute(
            "tapestry.commands.register({ name:'say', aliases:[\"'\"], roles:['player','mob'], handler:function(a,r){} });",
            "tapestry-core", "scripts/commands/say.js");
        rt.Execute(MobSay, "tapestry-core", "scripts/mobs/commands.js");

        policy.Resolve();

        var playerReg = reg.Resolve("say", "player");
        playerReg.Should().NotBeNull("the player verb must never lose to the mob-only registration");
        playerReg!.Roles.Should().Contain("player");
        reg.Resolve("'", "player").Should().NotBeNull();
        var mobReg = reg.Resolve("say", "mob");
        mobReg.Should().NotBeNull();
        mobReg!.Roles.Should().BeEquivalentTo(new[] { "mob" });
    }

    [Fact]
    public void TwoPacks_SameMobVerb_NoOverride_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        rt.Execute(MobSay, "pack-a", "scripts/a.js");
        rt.Execute(MobSay, "pack-b", "scripts/b.js");
        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("say").And.Contain("override");
    }

    [Fact]
    public void Override_WithDeclaredEdge_Wins()
    {
        var (rt, policy, reg, _) = BuildRuntime(new() { ["pack-b"] = new() { "pack-a" } });
        rt.Execute(MobSay, "pack-a", "scripts/a.js");
        rt.Execute("tapestry.mobs.registerCommand('say', { override: true, handler: function(mob, text) {} });",
            "pack-b", "scripts/b.js");
        policy.Resolve();
        var winner = reg.Resolve("say", "mob");
        winner.Should().NotBeNull();
        winner!.PackName.Should().Be("pack-b"); // winner identity, not just survival
    }

    [Fact]
    public void Override_WithoutEdge_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        rt.Execute(MobSay, "pack-a", "scripts/a.js");
        rt.Execute("tapestry.mobs.registerCommand('say', { override: true, handler: function(mob, text) {} });",
            "pack-b", "scripts/b.js");
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }
}
