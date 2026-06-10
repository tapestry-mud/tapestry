using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Registration;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class ProgressionTrackOverrideTests
{
    private static (JintRuntime rt, RegistrationPolicy policy, ProgressionManager progression, PackDependencyGraph graph)
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
                provider.GetRequiredService<ProgressionManager>(), graph);
    }

    private const string Combat =
        "tapestry.progression.registerTrack({ name:'combat', max_level: 50 });";
    private const string CombatB =
        "tapestry.progression.registerTrack({ name:'combat', max_level: 60 });";
    private const string CombatBOverride =
        "tapestry.progression.registerTrack({ name:'combat', max_level: 60, override:true });";

    [Fact]
    public void RegisterTrack_IsDeferred_UntilSeal()
    {
        var (rt, policy, progression, _) = BuildRuntime();
        rt.Execute(Combat, "pack-a", "scripts/a.js");
        progression.GetTrackDefinition("combat").Should().BeNull("tracks must commit at the seal, not eagerly");
        policy.Resolve();
        progression.GetTrackDefinition("combat").Should().NotBeNull();
    }

    [Fact]
    public void TwoPacks_SameTrack_NoOverride_BootError_NamingBothPacks()
    {
        var (rt, policy, _, _) = BuildRuntime();
        rt.Execute(Combat, "pack-a", "scripts/a.js");
        rt.Execute(CombatB, "pack-b", "scripts/b.js");
        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("combat").And.Contain("override")
            .And.Contain("pack-a").And.Contain("pack-b");
    }

    [Fact]
    public void Override_WithDeclaredEdge_Wins()
    {
        var (rt, policy, progression, _) = BuildRuntime(new() { ["pack-b"] = new() { "pack-a" } });
        rt.Execute(Combat, "pack-a", "scripts/a.js");
        rt.Execute(CombatBOverride, "pack-b", "scripts/b.js");
        policy.Resolve();
        progression.GetTrackDefinition("combat")!.MaxLevel.Should().Be(60,
            "the override's definition must win");
    }

    [Fact]
    public void Override_WithoutEdge_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        rt.Execute(Combat, "pack-a", "scripts/a.js");
        rt.Execute(CombatBOverride, "pack-b", "scripts/b.js");
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }
}
