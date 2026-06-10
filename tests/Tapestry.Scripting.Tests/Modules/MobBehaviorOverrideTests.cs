using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Registration;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class MobBehaviorOverrideTests
{
    private static (JintRuntime rt, RegistrationPolicy policy, MobAIManager mobAi, PackDependencyGraph graph)
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
                provider.GetRequiredService<MobAIManager>(), graph);
    }

    private const string GuardBehavior =
        "tapestry.mobs.registerBehavior('guard', function(ctx){});";

    [Fact]
    public void Behavior_IsDeferred_UntilSeal()
    {
        var (rt, policy, mobAi, _) = BuildRuntime();
        rt.Execute(GuardBehavior, "pack-a", "scripts/a.js");
        mobAi.HasBehavior("guard").Should().BeFalse("behaviors must commit at the seal, not eagerly");
        policy.Resolve();
        mobAi.HasBehavior("guard").Should().BeTrue();
    }

    [Fact]
    public void TwoPacks_SameBehavior_NoOverride_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        rt.Execute(GuardBehavior, "pack-a", "scripts/a.js");
        rt.Execute(GuardBehavior, "pack-b", "scripts/b.js");
        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("guard").And.Contain("override");
    }

    [Fact]
    public void Override_WithDeclaredEdge_Wins()
    {
        var (rt, policy, mobAi, _) = BuildRuntime(new() { ["pack-b"] = new() { "pack-a" } });
        rt.Execute(GuardBehavior, "pack-a", "scripts/a.js");
        rt.Execute("tapestry.mobs.registerBehavior('guard', function(ctx){}, { override: true });",
            "pack-b", "scripts/b.js");
        policy.Resolve();
        mobAi.HasBehavior("guard").Should().BeTrue();
    }

    [Fact]
    public void Override_WithoutEdge_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        rt.Execute(GuardBehavior, "pack-a", "scripts/a.js");
        rt.Execute("tapestry.mobs.registerBehavior('guard', function(ctx){}, { override: true });",
            "pack-b", "scripts/b.js");
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }
}
