using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Registration;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class ClassOverrideTests
{
    private static (JintRuntime rt, RegistrationPolicy policy, ClassRegistry reg, PackDependencyGraph graph)
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
                provider.GetRequiredService<ClassRegistry>(), graph);
    }

    private const string DefA = "tapestry.classes.register({ id:'warrior', name:'Warrior' });";
    private const string DefB = "tapestry.classes.register({ id:'warrior', name:'Berserker' });";
    private const string DefBOverride = "tapestry.classes.register({ id:'warrior', name:'Berserker', override:true });";

    [Fact]
    public void Register_IsDeferred_UntilSeal()
    {
        var (rt, policy, _, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", DefA, "scripts/a.js");

        EsmTest.Eval(rt, "tapestry.classes.get('warrior')").Should().BeNull();

        policy.Resolve();

        EsmTest.Eval(rt, "tapestry.classes.get('warrior').name").Should().Be("Warrior");
    }

    [Fact]
    public void TwoPacks_SameId_NoOverride_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", DefA, "scripts/a.js");
        EsmTest.Load(rt, "pack-b", DefB, "scripts/b.js");

        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("warrior").And.Contain("override")
            .And.Contain("pack-a").And.Contain("pack-b");
    }

    [Fact]
    public void Priority_NoLongerArbitrates_CollisionStillBootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", "tapestry.classes.register({ id:'warrior', name:'Warrior', priority:100 });", "scripts/a.js");
        EsmTest.Load(rt, "pack-b", "tapestry.classes.register({ id:'warrior', name:'Berserker', priority:1 });", "scripts/b.js");

        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }

    [Fact]
    public void Override_WithDeclaredEdge_Wins()
    {
        var (rt, policy, reg, _) = BuildRuntime(new() { ["pack-b"] = new() { "pack-a" } });
        EsmTest.Load(rt, "pack-a", DefA, "scripts/a.js");
        EsmTest.Load(rt, "pack-b", DefBOverride, "scripts/b.js");

        policy.Resolve();

        reg.Get("warrior")!.Name.Should().Be("Berserker");
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
