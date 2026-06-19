using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Color;
using Tapestry.Engine.Registration;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class ThemeOverrideTests
{
    private static (JintRuntime rt, RegistrationPolicy policy, ThemeRegistry themes, PackDependencyGraph graph)
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
                provider.GetRequiredService<ThemeRegistry>(), graph);
    }

    private const string WarningTheme =
        "tapestry.theme.register('ui.warning', { fg: 'red' });";

    [Fact]
    public void Theme_IsDeferred_UntilSeal()
    {
        var (rt, policy, themes, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", WarningTheme, "scripts/a.js");
        themes.IsKnown("ui.warning").Should().BeFalse("theme tags must commit at the seal, not eagerly");
        policy.Resolve();
        themes.IsKnown("ui.warning").Should().BeTrue();
    }

    [Fact]
    public void TwoPacks_SameTag_NoOverride_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", WarningTheme, "scripts/a.js");
        EsmTest.Load(rt, "pack-b", WarningTheme, "scripts/b.js");
        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("ui.warning")
            .And.Contain("pack-a")
            .And.Contain("pack-b");
    }

    [Fact]
    public void Override_WithDeclaredEdge_WinnerColorLands()
    {
        var (rt, policy, themes, _) = BuildRuntime(new() { ["pack-b"] = new() { "pack-a" } });
        EsmTest.Load(rt, "pack-a", WarningTheme, "scripts/a.js");
        EsmTest.Load(rt, "pack-b", "tapestry.theme.register('ui.warning', { fg: 'yellow', override: true });",
            "scripts/b.js");
        policy.Resolve();
        themes.Compile();
        var pair = themes.Resolve("ui.warning");
        pair.Should().NotBeNull();
        pair!.Open.Should().Contain("\x1b[33m", "the override's yellow must win, not the base's red");
    }

    [Fact]
    public void Override_WithoutEdge_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", WarningTheme, "scripts/a.js");
        EsmTest.Load(rt, "pack-b", "tapestry.theme.register('ui.warning', { fg: 'yellow', override: true });",
            "scripts/b.js");
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }
}
