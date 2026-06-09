using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Registration;
using Tapestry.Scripting;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class EssenceModuleTests
{
    private static (JintRuntime rt, RegistrationPolicy policy, PackDependencyGraph graph)
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
        return (rt, provider.GetRequiredService<RegistrationPolicy>(), graph);
    }

    [Fact]
    public void Register_AndGetEssence_ReturnsEssenceObject()
    {
        var (rt, policy, _) = BuildRuntime();
        rt.Execute("tapestry.essence.register({ key: 'fire', glyph: '^', color: 'red' });", "pack-a", "scripts/a.js");
        policy.Resolve();
        var result = rt.Evaluate("tapestry.essence.getEssence('fire').glyph;");
        Assert.Equal("^", result!.ToString());
    }

    [Fact]
    public void Format_KnownKey_ReturnsColoredGlyphInParens()
    {
        var (rt, policy, _) = BuildRuntime();
        rt.Execute("tapestry.essence.register({ key: 'fire', glyph: '^', color: 'red' });", "pack-a", "scripts/a.js");
        policy.Resolve();
        var result = rt.Evaluate("tapestry.essence.format('fire');");
        Assert.NotNull(result);
        Assert.Contains("(^)", result!.ToString());
    }

    [Fact]
    public void Format_NullKey_ReturnsEmptyString()
    {
        var (rt, policy, _) = BuildRuntime();
        rt.Execute("tapestry.essence.register({ key: 'fire', glyph: '^', color: 'red' });", "pack-a", "scripts/a.js");
        policy.Resolve();
        var result = rt.Evaluate("tapestry.essence.format(null);");
        Assert.Equal(string.Empty, result!.ToString());
    }

    [Fact]
    public void Format_UnknownKey_ReturnsEmptyString()
    {
        var (rt, policy, _) = BuildRuntime();
        policy.Resolve();
        var result = rt.Evaluate("tapestry.essence.format('void');");
        Assert.Equal(string.Empty, result!.ToString());
    }

    [Fact]
    public void TwoPacks_SameEssence_NoOverride_BootError()
    {
        var (rt, policy, _) = BuildRuntime();
        rt.Execute("tapestry.essence.register({ key:'fire', glyph:'^', color:'red' });", "pack-a", "scripts/a.js");
        rt.Execute("tapestry.essence.register({ key:'fire', glyph:'#', color:'orange' });", "pack-b", "scripts/b.js");
        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("fire").And.Contain("override");
    }

    [Fact]
    public void Override_WithDeclaredEdge_Wins()
    {
        var (rt, policy, _) = BuildRuntime(new() { ["pack-b"] = new() { "pack-a" } });
        rt.Execute("tapestry.essence.register({ key:'fire', glyph:'^', color:'red' });", "pack-a", "scripts/a.js");
        rt.Execute("tapestry.essence.register({ key:'fire', glyph:'#', color:'orange', override:true });", "pack-b", "scripts/b.js");
        policy.Resolve();
        var result = rt.Evaluate("tapestry.essence.getEssence('fire').glyph;");
        Assert.Equal("#", result!.ToString());
    }

    [Fact]
    public void Override_WithoutEdge_BootError()
    {
        var (rt, policy, _) = BuildRuntime(); // no edges
        rt.Execute("tapestry.essence.register({ key:'fire', glyph:'^', color:'red' });", "pack-a", "scripts/a.js");
        rt.Execute("tapestry.essence.register({ key:'fire', glyph:'#', color:'orange', override:true });", "pack-b", "scripts/b.js");
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }
}
