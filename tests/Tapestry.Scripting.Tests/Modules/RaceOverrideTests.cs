using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Races;
using Tapestry.Engine.Registration;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class RaceOverrideTests
{
    private static (JintRuntime rt, RegistrationPolicy policy, RaceRegistry reg, PackDependencyGraph graph)
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
                provider.GetRequiredService<RaceRegistry>(), graph);
    }

    private const string DefA = "tapestry.races.register({ id:'human', name:'Human' });";
    private const string DefB = "tapestry.races.register({ id:'human', name:'Highlander' });";
    private const string DefBOverride = "tapestry.races.register({ id:'human', name:'Highlander', override:true });";

    [Fact]
    public void Register_IsDeferred_UntilSeal()
    {
        var (rt, policy, _, _) = BuildRuntime();
        rt.Execute(DefA, "pack-a", "scripts/a.js");

        rt.Evaluate("tapestry.races.get('human')").Should().BeNull();

        policy.Resolve();

        rt.Evaluate("tapestry.races.get('human').name").Should().Be("Human");
    }

    [Fact]
    public void TwoPacks_SameId_NoOverride_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        rt.Execute(DefA, "pack-a", "scripts/a.js");
        rt.Execute(DefB, "pack-b", "scripts/b.js");

        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("human").And.Contain("override")
            .And.Contain("pack-a").And.Contain("pack-b");
    }

    [Fact]
    public void Priority_NoLongerArbitrates_CollisionStillBootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        rt.Execute("tapestry.races.register({ id:'human', name:'Human', priority:100 });", "pack-a", "scripts/a.js");
        rt.Execute("tapestry.races.register({ id:'human', name:'Highlander', priority:1 });", "pack-b", "scripts/b.js");

        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }

    [Fact]
    public void Override_WithDeclaredEdge_Wins()
    {
        var (rt, policy, reg, _) = BuildRuntime(new() { ["pack-b"] = new() { "pack-a" } });
        rt.Execute(DefA, "pack-a", "scripts/a.js");
        rt.Execute(DefBOverride, "pack-b", "scripts/b.js");

        policy.Resolve();

        reg.Get("human")!.Name.Should().Be("Highlander");
    }

    [Fact]
    public void Override_WithoutEdge_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime(); // no edges
        rt.Execute(DefA, "pack-a", "scripts/a.js");
        rt.Execute(DefBOverride, "pack-b", "scripts/b.js");

        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }
}
