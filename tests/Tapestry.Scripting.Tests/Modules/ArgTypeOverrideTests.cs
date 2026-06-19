using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Registration;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class ArgTypeOverrideTests
{
    private static (JintRuntime rt, RegistrationPolicy policy, ArgResolver resolver, PackDependencyGraph graph)
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
                provider.GetRequiredService<ArgResolver>(), graph);
    }

    [Fact]
    public void SingleArgType_DefObject_RegistersAndResolves_AfterSeal()
    {
        var (rt, policy, resolver, _) = BuildRuntime();
        EsmTest.Load(rt, "tapestry-tinkers",
            "tapestry.args.registerType({ name:'recipe', resolve:function(actor, token, def){ return { success:true, value:'recipe:'+token }; } });",
            "scripts/00-recipes.js");

        // Deferral: the candidate is only committed at the seal. Before Resolve(), _packTypes is empty.
        policy.Resolve();

        var actor = new ActorContext { EntityId = Guid.NewGuid(), RoomId = null };
        var def = new ArgDefinition { Type = "recipe", Required = true };
        var (success, value, _) = resolver.ResolveToken(actor, "item", def, "bread");
        success.Should().BeTrue();
        value.Should().Be("recipe:bread");
    }

    [Fact]
    public void TwoPacks_SameArgType_NoOverride_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", "tapestry.args.registerType({ name:'recipe', resolve:function(a,t,d){ return { success:true, value:t }; } });", "scripts/a.js");
        EsmTest.Load(rt, "pack-b", "tapestry.args.registerType({ name:'recipe', resolve:function(a,t,d){ return { success:true, value:t }; } });", "scripts/b.js");

        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("recipe").And.Contain("override");
    }

    [Fact]
    public void Override_WithDeclaredEdge_Wins()
    {
        var (rt, policy, resolver, _) = BuildRuntime(new() { ["pack-b"] = new() { "pack-a" } });
        EsmTest.Load(rt, "pack-a", "tapestry.args.registerType({ name:'recipe', resolve:function(a,t,d){ return { success:true, value:'A' }; } });", "scripts/a.js");
        EsmTest.Load(rt, "pack-b", "tapestry.args.registerType({ name:'recipe', override:true, resolve:function(a,t,d){ return { success:true, value:'B' }; } });", "scripts/b.js");

        policy.Resolve();

        var actor = new ActorContext { EntityId = Guid.NewGuid(), RoomId = null };
        var def = new ArgDefinition { Type = "recipe", Required = true };
        var (success, value, _) = resolver.ResolveToken(actor, "item", def, "x");
        success.Should().BeTrue();
        value.Should().Be("B");
    }

    [Fact]
    public void Override_WithoutEdge_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime(); // no edges
        EsmTest.Load(rt, "pack-a", "tapestry.args.registerType({ name:'recipe', resolve:function(a,t,d){ return { success:true, value:t }; } });", "scripts/a.js");
        EsmTest.Load(rt, "pack-b", "tapestry.args.registerType({ name:'recipe', override:true, resolve:function(a,t,d){ return { success:true, value:t }; } });", "scripts/b.js");
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }
}
