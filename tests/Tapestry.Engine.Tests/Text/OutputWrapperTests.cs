using FluentAssertions;
using Tapestry.Engine.Text;

namespace Tapestry.Engine.Tests.Text;

public class OutputWrapperTests
{
    private static readonly OutputWrapper W = new();

    private const string Red = "\x1b[31m";
    private const string Reset = "\x1b[0m";

    private static IEnumerable<string> Lines(string s) => s.Split("\r\n");

    [Fact]
    public void UnderWidth_Unchanged()
    {
        W.Wrap("the quick brown fox", 80).Should().Be("the quick brown fox");
    }

    [Fact]
    public void BreaksAtLastSpace_NeverMidWord()
    {
        W.Wrap("the quick brown fox", 9).Should().Be("the quick\r\nbrown fox");
    }

    [Fact]
    public void KeepsWordWhole_WhenItDoesNotFit()
    {
        W.Wrap("hello wonderful", 11).Should().Be("hello\r\nwonderful");
    }

    [Fact]
    public void NeverInsertsHyphen()
    {
        W.Wrap("antidisestablishmentarianism is long", 10).Should().NotContain("-");
    }

    [Fact]
    public void OverLongWord_OverflowsWhole_NotSplit()
    {
        // A single token longer than the width is emitted intact (overflow), never split.
        W.Wrap("supercalifragilistic word", 10).Should().Be("supercalifragilistic\r\nword");
    }

    [Fact]
    public void EveryWrapPoint_WithinWidth_ForLongProse()
    {
        var prose = "It is a truth universally acknowledged that a single man in possession " +
                    "of a good fortune must be in want of a wife.";
        // No token exceeds 30, so every line fits.
        foreach (var line in Lines(W.Wrap(prose, 30)))
        {
            line.Length.Should().BeLessThanOrEqualTo(30);
        }
    }

    [Fact]
    public void AnsiEscapes_AreZeroWidth()
    {
        // Visible text is "red quick brown" -> wraps as if the escapes were not there.
        var input = Red + "red quick brown" + Reset;
        W.Wrap(input, 9).Should().Be(Red + "red quick\r\nbrown" + Reset);
    }

    [Fact]
    public void VisibleLength_IgnoresEscapes()
    {
        W.VisibleLength(Red + "abc" + Reset).Should().Be(3);
        W.VisibleLength("\x1b[1;97mbold\x1b[0m text").Should().Be(9); // "bold text"
        W.VisibleLength("plain").Should().Be(5);
    }

    [Fact]
    public void EmbeddedNewlines_WrappedIndependently()
    {
        W.Wrap("alpha beta\r\ngamma delta", 5).Should().Be("alpha\r\nbeta\r\ngamma\r\ndelta");
    }

    [Fact]
    public void BlankLines_Preserved()
    {
        W.Wrap("a\r\n\r\nb", 80).Should().Be("a\r\n\r\nb");
    }

    [Fact]
    public void TrailingNewline_Preserved()
    {
        W.Wrap("hello\r\n", 80).Should().Be("hello\r\n");
    }

    [Fact]
    public void LoneNewline_NormalizedToCrlf()
    {
        W.Wrap("a\nb", 80).Should().Be("a\r\nb");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveWidth_Passthrough(int width)
    {
        W.Wrap("the quick brown fox jumps over", width).Should().Be("the quick brown fox jumps over");
    }

    [Fact]
    public void Empty_ReturnsEmpty()
    {
        W.Wrap("", 80).Should().Be("");
    }

    [Fact]
    public void SpacelessLine_OverflowsRatherThanSplitting()
    {
        // A border-like line with no spaces is left intact (protects panels/maps from
        // mid-glyph splits) rather than hard-broken.
        var border = new string('=', 40);
        W.Wrap(border, 20).Should().Be(border);
    }
}
