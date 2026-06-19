using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Registration;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class CommandOverrideTests
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

    [Fact]
    public void TwoPacks_SameCommand_NoOverride_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", "tapestry.commands.register({ name:'look', handler:function(a,r){} });", "scripts/a.js");
        EsmTest.Load(rt, "pack-b", "tapestry.commands.register({ name:'look', handler:function(a,r){} });", "scripts/b.js");

        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("look").And.Contain("override");
    }

    [Fact]
    public void Override_WithDeclaredEdge_Wins()
    {
        var (rt, policy, reg, _) = BuildRuntime(new() { ["pack-b"] = new() { "pack-a" } });
        EsmTest.Load(rt, "pack-a", "tapestry.commands.register({ name:'look', handler:function(a,r){} });", "scripts/a.js");
        EsmTest.Load(rt, "pack-b", "tapestry.commands.register({ name:'look', override:true, handler:function(a,r){} });", "scripts/b.js");

        policy.Resolve();
        reg.Resolve("look")!.PackName.Should().Be("pack-b");
    }

    [Fact]
    public void Override_WithoutEdge_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime(); // no edges
        EsmTest.Load(rt, "pack-a", "tapestry.commands.register({ name:'look', handler:function(a,r){} });", "scripts/a.js");
        EsmTest.Load(rt, "pack-b", "tapestry.commands.register({ name:'look', override:true, handler:function(a,r){} });", "scripts/b.js");
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }

    [Fact]
    public void SingleCommand_Registers_Normally()
    {
        var (rt, policy, reg, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", "tapestry.commands.register({ name:'wave', handler:function(a,r){} });", "scripts/a.js");
        policy.Resolve();
        reg.Resolve("wave").Should().NotBeNull();
    }
}
