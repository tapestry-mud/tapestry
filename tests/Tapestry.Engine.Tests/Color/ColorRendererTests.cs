// tests/Tapestry.Engine.Tests/Color/ColorRendererTests.cs
using FluentAssertions;
using Tapestry.Engine.Color;

namespace Tapestry.Engine.Tests.Color;

public class ColorRendererTests
{
    private ColorRenderer CreateRenderer()
    {
        var theme = new ThemeRegistry();
        theme.Register("highlight", new ThemeEntry { Fg = "bright-white" });
        theme.Register("danger", new ThemeEntry { Fg = "bright-red" });
        theme.Register("npc", new ThemeEntry { Fg = "bright-cyan" });
        theme.Register("item.common", new ThemeEntry { Fg = "white" });
        theme.Register("item.artifact", new ThemeEntry { Fg = "bright-yellow" });
        theme.Compile();
        return new ColorRenderer(theme);
    }

    [Fact]
    public void PlainText_PassesThrough()
    {
        var renderer = CreateRenderer();
        renderer.RenderAnsi("Hello world").Should().Be("Hello world");
    }

    [Fact]
    public void SemanticTag_RendersAnsi()
    {
        var renderer = CreateRenderer();
        var result = renderer.RenderAnsi("<highlight>Town Square</highlight>");
        result.Should().Be("\x1b[97mTown Square\x1b[0m");
    }

    [Fact]
    public void MultipleTags_InOneLine()
    {
        var renderer = CreateRenderer();
        var result = renderer.RenderAnsi("You see <npc>Mira</npc> near a <item.common>sword</item.common>.");
        result.Should().Be("You see \x1b[96mMira\x1b[0m near a \x1b[37msword\x1b[0m.");
    }

    [Fact]
    public void UnknownTag_PassesThroughUnchanged()
    {
        var renderer = CreateRenderer();
        var result = renderer.RenderAnsi("<unknown>text</unknown>");
        result.Should().Be("<unknown>text</unknown>");
    }

    [Fact]
    public void LiteralColor_RendersAnsi()
    {
        var renderer = CreateRenderer();
        var result = renderer.RenderAnsi("<color fg=\"bright-yellow\">gold text</color>");
        result.Should().Be("\x1b[93mgold text\x1b[0m");
    }

    [Fact]
    public void LiteralColor_FgAndBg()
    {
        var renderer = CreateRenderer();
        var result = renderer.RenderAnsi("<color fg=\"bright-white\" bg=\"red\">alert</color>");
        result.Should().Contain("\x1b[97m");
        result.Should().Contain("\x1b[41m");
        result.Should().EndWith("alert\x1b[0m");
    }

    [Fact]
    public void StripMode_RemovesAllTags()
    {
        var renderer = CreateRenderer();
        var result = renderer.RenderPlain("<highlight>Town</highlight> has <danger>danger</danger>");
        result.Should().Be("Town has danger");
    }

    [Fact]
    public void CachedResult_ReturnsSameString()
    {
        var renderer = CreateRenderer();
        var input = "<highlight>test</highlight>";
        var result1 = renderer.RenderAnsi(input);
        var result2 = renderer.RenderAnsi(input);
        result1.Should().Be(result2);
        ReferenceEquals(result1, result2).Should().BeTrue();
    }

    [Fact]
    public void UnknownTag_PlainRender_PassesThroughUnchanged()
    {
        var renderer = CreateRenderer();
        var result = renderer.RenderPlain("<slash>text</slash>");
        result.Should().Be("<slash>text</slash>");
    }

    [Fact]
    public void AsciiDecoratorsInsideKnownTag_PreservedAsContent()
    {
        var renderer = CreateRenderer();
        // Decorators are opaque content inside a known tag — not re-parsed
        var result = renderer.RenderAnsi("<danger><<<--->>></danger>");
        result.Should().Be("\x1b[91m<<<--->>>\x1b[0m");
    }

    [Fact]
    public void KnownTagNoClosingTag_FallsBackToLiteralBracket()
    {
        var renderer = CreateRenderer();
        // Known tag but no closing tag — fall back to literal < at that position
        var result = renderer.RenderAnsi("<highlight>orphan");
        result.Should().StartWith("<");
        result.Should().Contain("orphan");
    }
}
