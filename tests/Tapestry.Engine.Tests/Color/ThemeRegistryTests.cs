// tests/Tapestry.Engine.Tests/Color/ThemeRegistryTests.cs
using FluentAssertions;
using Tapestry.Engine.Color;

namespace Tapestry.Engine.Tests.Color;

public class ThemeRegistryTests
{
    [Fact]
    public void Register_AndResolve_ReturnsAnsiPair()
    {
        var registry = new ThemeRegistry();
        registry.Register("highlight", new ThemeEntry { Fg = "bright-white" });
        registry.Compile();

        var pair = registry.Resolve("highlight");
        pair.Should().NotBeNull();
        pair!.Open.Should().Be("\x1b[97m");
        pair.Close.Should().Be("\x1b[0m");
    }

    [Fact]
    public void Resolve_UnknownTag_ReturnsNull()
    {
        var registry = new ThemeRegistry();
        registry.Compile();
        registry.Resolve("nonexistent").Should().BeNull();
    }

    [Fact]
    public void Compile_FreezesMappings()
    {
        var registry = new ThemeRegistry();
        registry.Register("danger", new ThemeEntry { Fg = "bright-red" });
        registry.Compile();

        var pair = registry.Resolve("danger");
        pair.Should().NotBeNull();
        pair!.Open.Should().Be("\x1b[91m");
    }

    [Fact]
    public void Specificity_MoreSpecificWins()
    {
        var registry = new ThemeRegistry();
        registry.Register("item", new ThemeEntry { Fg = "white" });
        registry.Register("item.artifact", new ThemeEntry { Fg = "bright-yellow" });
        registry.Compile();

        registry.Resolve("item.artifact")!.Open.Should().Be("\x1b[93m");
        registry.Resolve("item")!.Open.Should().Be("\x1b[37m");
    }

    [Fact]
    public void FgAndBg_Combined()
    {
        var registry = new ThemeRegistry();
        registry.Register("alert", new ThemeEntry { Fg = "bright-white", Bg = "red" });
        registry.Compile();

        var pair = registry.Resolve("alert");
        pair!.Open.Should().Contain("\x1b[97m");
        pair.Open.Should().Contain("\x1b[41m");
    }

    [Fact]
    public void IsKnown_RegisteredTag_ReturnsTrue()
    {
        var registry = new ThemeRegistry();
        registry.Register("highlight", new ThemeEntry { Fg = "bright-white" });
        registry.IsKnown("highlight").Should().BeTrue();
    }

    [Fact]
    public void IsKnown_UnregisteredTag_ReturnsFalse()
    {
        var registry = new ThemeRegistry();
        registry.IsKnown("slash").Should().BeFalse();
    }

    [Fact]
    public void IsKnown_IsCaseInsensitive()
    {
        var registry = new ThemeRegistry();
        registry.Register("highlight", new ThemeEntry { Fg = "bright-white" });
        registry.IsKnown("HIGHLIGHT").Should().BeTrue();
    }

    [Fact]
    public void IsKnown_DoesNotRequireCompile()
    {
        var registry = new ThemeRegistry();
        registry.Register("highlight", new ThemeEntry { Fg = "bright-white" });
        // IsKnown checks _entries (registered set), not _compiled (post-Compile set)
        registry.IsKnown("highlight").Should().BeTrue();
    }

    [Fact]
    public void GetHtmlMap_ReturnsOnlyEntriesWithHtml()
    {
        var registry = new ThemeRegistry();
        registry.Register("item.rare", new ThemeEntry { Fg = "green", Html = "text-green-400" });
        registry.Register("item.common", new ThemeEntry { Fg = "white" });

        var map = registry.GetHtmlMap();

        map.Should().ContainKey("item.rare");
        map["item.rare"].Should().Be("text-green-400");
        map.Should().NotContainKey("item.common");
    }

    [Fact]
    public void GetHtmlMap_IsCaseInsensitiveOnRead()
    {
        var registry = new ThemeRegistry();
        registry.Register("item.RARE", new ThemeEntry { Fg = "green", Html = "text-green-400" });

        var map = registry.GetHtmlMap();

        map.Should().ContainKey("item.rare");
    }
}
