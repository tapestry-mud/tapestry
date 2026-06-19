using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Registration;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class FlowOverrideTests
{
    private static (JintRuntime rt, RegistrationPolicy policy, FlowRegistry flows, PackDependencyGraph graph)
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
                provider.GetRequiredService<FlowRegistry>(), graph);
    }

    private const string Creation =
        "tapestry.flows.register({ id:'character_creation', display_name:'Creation A', trigger:'new_player_connect', steps:[] });";
    private const string CreationB =
        "tapestry.flows.register({ id:'character_creation', display_name:'Creation B', trigger:'new_player_connect', steps:[] });";
    private const string CreationBOverride =
        "tapestry.flows.register({ id:'character_creation', display_name:'Creation B', trigger:'new_player_connect', steps:[], override:true });";

    [Fact]
    public void Register_IsDeferred_UntilSeal()
    {
        var (rt, policy, flows, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", Creation, "scripts/a.js");
        flows.Get("character_creation").Should().BeNull("flows must commit at the seal, not eagerly");
        policy.Resolve();
        flows.Get("character_creation").Should().NotBeNull();
    }

    [Fact]
    public void TwoPacks_SameFlow_NoOverride_BootError_NamingBothPacks()
    {
        var (rt, policy, _, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", Creation, "scripts/a.js");
        EsmTest.Load(rt, "pack-b", CreationB, "scripts/b.js");
        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("character_creation").And.Contain("override")
            .And.Contain("pack-a").And.Contain("pack-b");
    }

    [Fact]
    public void Override_WithDeclaredEdge_Wins()
    {
        var (rt, policy, flows, _) = BuildRuntime(new() { ["pack-b"] = new() { "pack-a" } });
        EsmTest.Load(rt, "pack-a", Creation, "scripts/a.js");
        EsmTest.Load(rt, "pack-b", CreationBOverride, "scripts/b.js");
        policy.Resolve();
        flows.Get("character_creation")!.DisplayName.Should().Be("Creation B",
            "the override's definition must win");
    }

    [Fact]
    public void Override_WithoutEdge_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", Creation, "scripts/a.js");
        EsmTest.Load(rt, "pack-b", CreationBOverride, "scripts/b.js");
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }
}
