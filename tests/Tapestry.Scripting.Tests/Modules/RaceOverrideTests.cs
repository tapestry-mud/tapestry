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
        EsmTest.Load(rt, "pack-a", DefA, "scripts/a.js");

        EsmTest.Eval(rt, "tapestry.races.get('human')").Should().BeNull();

        policy.Resolve();

        EsmTest.Eval(rt, "tapestry.races.get('human').name").Should().Be("Human");
    }

    [Fact]
    public void TwoPacks_SameId_NoOverride_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", DefA, "scripts/a.js");
        EsmTest.Load(rt, "pack-b", DefB, "scripts/b.js");

        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("human").And.Contain("override")
            .And.Contain("pack-a").And.Contain("pack-b");
    }

    [Fact]
    public void Priority_NoLongerArbitrates_CollisionStillBootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", "tapestry.races.register({ id:'human', name:'Human', priority:100 });", "scripts/a.js");
        EsmTest.Load(rt, "pack-b", "tapestry.races.register({ id:'human', name:'Highlander', priority:1 });", "scripts/b.js");

        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }

    [Fact]
    public void Override_WithDeclaredEdge_Wins()
    {
        var (rt, policy, reg, _) = BuildRuntime(new() { ["pack-b"] = new() { "pack-a" } });
        EsmTest.Load(rt, "pack-a", DefA, "scripts/a.js");
        EsmTest.Load(rt, "pack-b", DefBOverride, "scripts/b.js");

        policy.Resolve();

        reg.Get("human")!.Name.Should().Be("Highlander");
    }

    [Fact]
    public void Override_WithoutEdge_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime(); // no edges
        EsmTest.Load(rt, "pack-a", DefA, "scripts/a.js");
        EsmTest.Load(rt, "pack-b", DefBOverride, "scripts/b.js");

        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }
}
