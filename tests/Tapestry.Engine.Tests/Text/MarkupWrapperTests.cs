using FluentAssertions;
using Tapestry.Engine.Color;
using Tapestry.Engine.Text;

namespace Tapestry.Engine.Tests.Text;

public class MarkupWrapperTests
{
    private static ThemeRegistry Theme()
    {
        var theme = new ThemeRegistry();
        theme.Register("highlight", new ThemeEntry { Fg = "bright-white" });
        theme.Register("danger", new ThemeEntry { Fg = "bright-red" });
        theme.Register("direction", new ThemeEntry { Fg = "cyan" });
        theme.Register("npc", new ThemeEntry { Fg = "bright-cyan" });
        theme.Register("item.common", new ThemeEntry { Fg = "white" });
        theme.Compile();
        return theme;
    }

    private static MarkupWrapper Wrapper() => new(Theme());

    private static IEnumerable<string> Lines(string s) => s.Split("\r\n");

    [Fact]
    public void UnderWidth_Unchanged()
    {
        Wrapper().Wrap("the quick brown fox", 80).Should().Be("the quick brown fox");
    }

    [Fact]
    public void BreaksAtLastSpace_NeverMidWord()
    {
        Wrapper().Wrap("the quick brown fox", 9).Should().Be("the quick\r\nbrown fox");
    }

    [Fact]
    public void KeepsWordWhole_WhenItDoesNotFit()
    {
        // "wonderful" (9) would overflow 11 if appended to "hello"; it moves whole.
        Wrapper().Wrap("hello wonderful", 11).Should().Be("hello\r\nwonderful");
    }

    [Fact]
    public void NeverInsertsHyphen()
    {
        var result = Wrapper().Wrap("antidisestablishmentarianism is long", 10);
        result.Should().NotContain("-");
    }

    [Fact]
    public void OverLongWord_HardBreaksToHonorWidth()
    {
        Wrapper().Wrap("abcdefghij", 4).Should().Be("abcd\r\nefgh\r\nij");
    }

    [Fact]
    public void EveryLine_WithinWidth_ForLongProse()
    {
        var prose = "It is a truth universally acknowledged that a single man in possession " +
                    "of a good fortune must be in want of a wife.";
        var result = Wrapper().Wrap(prose, 30);
        foreach (var line in Lines(result))
        {
            line.Length.Should().BeLessThanOrEqualTo(30);
        }
    }

    [Fact]
    public void Markup_IsZeroWidth_WrapsOnVisibleText()
    {
        Wrapper().Wrap("<highlight>the quick brown fox</highlight>", 9)
            .Should().Be("<highlight>the quick\r\nbrown fox</highlight>");
    }

    [Fact]
    public void BraceColor_IsZeroWidth()
    {
        // Visible text is "red text here" (13). At width 8 it wraps after "text".
        Wrapper().Wrap("{red}red text here{/}", 8)
            .Should().Be("{red}red text\r\nhere{/}");
    }

    [Fact]
    public void ColorTagWithSpaces_NotSplitAtInternalSpace()
    {
        // <color fg="red"> contains a space but must be treated as one zero-width unit.
        Wrapper().Wrap("<color fg=\"red\">hello world</color>", 7)
            .Should().Be("<color fg=\"red\">hello\r\nworld</color>");
    }

    [Fact]
    public void EmbeddedNewlines_WrappedIndependently()
    {
        Wrapper().Wrap("alpha beta\r\ngamma delta", 5)
            .Should().Be("alpha\r\nbeta\r\ngamma\r\ndelta");
    }

    [Fact]
    public void BlankLines_Preserved()
    {
        Wrapper().Wrap("a\r\n\r\nb", 80).Should().Be("a\r\n\r\nb");
    }

    [Fact]
    public void TrailingNewline_Preserved()
    {
        Wrapper().Wrap("hello\r\n", 80).Should().Be("hello\r\n");
    }

    [Fact]
    public void LoneNewline_NormalizedToCrlf()
    {
        Wrapper().Wrap("a\nb", 80).Should().Be("a\r\nb");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveWidth_Passthrough(int width)
    {
        Wrapper().Wrap("the quick brown fox jumps over", width)
            .Should().Be("the quick brown fox jumps over");
    }

    [Fact]
    public void Empty_ReturnsEmpty()
    {
        Wrapper().Wrap("", 80).Should().Be("");
    }

    [Fact]
    public void Output_IsAsciiOnly()
    {
        var result = Wrapper().Wrap("<highlight>plain ascii prose wraps cleanly here</highlight>", 12);
        result.Should().MatchRegex("^[\\x00-\\x7E]*$");
    }

    // --- Drift guard: visible length must match what ColorRenderer actually strips ---

    [Theory]
    [InlineData("plain text")]
    [InlineData("<highlight>hi there</highlight>")]
    [InlineData("{yellow}gold{/}")]
    [InlineData("<color fg=\"red\">x y z</color>")]
    [InlineData("a {bold}b{/} <danger>c</danger> d")]
    [InlineData("mix <npc>Mira</npc> and {cyan}blue{/} text")]
    [InlineData("unknown <foo>tag</foo> and {notacolor} brace")]
    [InlineData("less than 5 < 10 and 8 > 3 math")]
    public void VisibleLength_MatchesColorRendererStrip(string input)
    {
        var theme = Theme();
        var renderer = new ColorRenderer(theme);
        var wrapper = new MarkupWrapper(theme);

        wrapper.VisibleLength(input).Should().Be(renderer.RenderPlain(input).Length);
    }
}
