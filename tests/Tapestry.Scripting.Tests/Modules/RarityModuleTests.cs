using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Registration;
using Tapestry.Scripting;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class RarityModuleTests
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
    public void Register_AndGetTier_ReturnsTierObject()
    {
        var (rt, policy, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", @"tapestry.rarity.register({ key: 'rare', order: 2, displayText: 'Rare',
            decorators: { left: '-= ', right: ' =-' }, color: 'green', visible: true });",
            "scripts/a.js");
        policy.Resolve();
        var result = EsmTest.Eval(rt, "tapestry.rarity.getTier('rare').key");
        Assert.Equal("rare", result!.ToString());
    }

    [Fact]
    public void Format_VisibleTier_ReturnsNonEmptyString()
    {
        var (rt, policy, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", @"tapestry.rarity.register({ key: 'rare', order: 2, displayText: 'Rare',
            decorators: { left: '-= ', right: ' =-' }, color: 'green', visible: true });",
            "scripts/a.js");
        policy.Resolve();
        var result = EsmTest.Eval(rt, "tapestry.rarity.format('rare')");
        Assert.NotNull(result);
        Assert.Contains("Rare", result!.ToString());
    }

    [Fact]
    public void Format_InvisibleTier_ReturnsWhitespace()
    {
        var (rt, policy, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", @"
            tapestry.rarity.register({ key: 'common', order: 0, displayText: null,
                decorators: null, color: 'white', visible: false });
            tapestry.rarity.register({ key: 'uncommon', order: 1, displayText: 'Uncommon',
                decorators: { left: '-= ', right: ' =-' }, color: 'white', visible: true });",
            "scripts/a.js");
        policy.Resolve();
        var result = EsmTest.Eval(rt, "tapestry.rarity.format('common')");
        Assert.NotNull(result);
        Assert.Equal(new string(' ', 14), result!.ToString());
    }

    [Fact]
    public void TagWidth_ReturnsZeroWhenNothingRegistered()
    {
        var (rt, policy, _) = BuildRuntime();
        policy.Resolve();
        var result = EsmTest.Eval(rt, "tapestry.rarity.tagWidth()");
        Assert.Equal(0, Convert.ToInt32(result));
    }

    [Fact]
    public void TagWidth_ReturnsMaxRenderedWidth()
    {
        var (rt, policy, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", @"
            tapestry.rarity.register({ key: 'uncommon', order: 1, displayText: 'Uncommon',
                decorators: { left: '-= ', right: ' =-' }, color: 'white', visible: true });
            tapestry.rarity.register({ key: 'rare', order: 2, displayText: 'Rare',
                decorators: { left: '-= ', right: ' =-' }, color: 'green', visible: true });",
            "scripts/a.js");
        policy.Resolve();
        var result = EsmTest.Eval(rt, "tapestry.rarity.tagWidth()");
        Assert.Equal(14, Convert.ToInt32(result));
    }

    [Fact]
    public void TwoPacks_SameRarity_NoOverride_BootError()
    {
        var (rt, policy, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", "tapestry.rarity.register({ key:'rare', order:2, color:'green', visible:true });", "scripts/a.js");
        EsmTest.Load(rt, "pack-b", "tapestry.rarity.register({ key:'rare', order:2, color:'cyan', visible:true });", "scripts/b.js");
        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("rare").And.Contain("override");
    }

    [Fact]
    public void Override_WithDeclaredEdge_Wins()
    {
        var (rt, policy, _) = BuildRuntime(new() { ["pack-b"] = new() { "pack-a" } });
        EsmTest.Load(rt, "pack-a", "tapestry.rarity.register({ key:'rare', order:2, color:'green', visible:true });", "scripts/a.js");
        EsmTest.Load(rt, "pack-b", "tapestry.rarity.register({ key:'rare', order:2, color:'cyan', visible:true, override:true });", "scripts/b.js");
        policy.Resolve();
        var result = EsmTest.Eval(rt, "tapestry.rarity.getTier('rare').color");
        Assert.Equal("cyan", result!.ToString());
    }

    [Fact]
    public void Override_WithoutEdge_BootError()
    {
        var (rt, policy, _) = BuildRuntime(); // no edges
        EsmTest.Load(rt, "pack-a", "tapestry.rarity.register({ key:'rare', order:2, color:'green', visible:true });", "scripts/a.js");
        EsmTest.Load(rt, "pack-b", "tapestry.rarity.register({ key:'rare', order:2, color:'cyan', visible:true, override:true });", "scripts/b.js");
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }
}
