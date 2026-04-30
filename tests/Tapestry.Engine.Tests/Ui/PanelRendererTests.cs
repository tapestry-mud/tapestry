using FluentAssertions;
using System.Linq;
using Tapestry.Engine.Color;
using Tapestry.Engine.Ui;

namespace Tapestry.Engine.Tests.Ui;

public class PanelRendererTests
{
    private static PanelRenderer Renderer() => new();

    private static PanelRenderer RendererWithTheme(params string[] tags)
    {
        var theme = new ThemeRegistry();
        foreach (var tag in tags)
        {
            theme.Register(tag, new ThemeEntry { Fg = "cyan" });
        }
        theme.Compile();
        return new PanelRenderer(theme);
    }

    private static string[] Lines(string rendered) =>
        rendered.Split("\r\n");

    // ── Structural ─────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyPanel_RendersTwoMajorBorders()
    {
        var panel = new Panel { Width = 10, Sections = Array.Empty<Section>() };
        var lines = Lines(Renderer().Render(panel));
        lines.Should().HaveCount(2);
        TagStripper.StripTags(lines[0]).Should().Be("|========|");
        TagStripper.StripTags(lines[1]).Should().Be("|========|");
    }

    [Fact]
    public void TopAndBottomBordersAlwaysMajor()
    {
        var panel = new Panel
        {
            Width = 6,
            Sections = new[]
            {
                new Section { Rows = new Row[] { new EmptyRow() } }
            }
        };
        var lines = Lines(Renderer().Render(panel));
        TagStripper.StripTags(lines[0]).Should().Be("|====|");
        TagStripper.StripTags(lines[^1]).Should().Be("|====|");
    }

    [Fact]
    public void EveryLineIsExactlyPanelWidth()
    {
        var panel = new Panel
        {
            Width = 47,
            Sections = new[]
            {
                new Section
                {
                    Rows = new Row[]
                    {
                        new EmptyRow(),
                        new TextRow { Content = " Hello" },
                        new EmptyRow()
                    }
                },
                new Section
                {
                    SeparatorAbove = RuleStyle.Minor,
                    Rows = new Row[] { new EmptyRow() }
                }
            }
        };
        var lines = Lines(Renderer().Render(panel));
        lines.Should().OnlyContain(l => TagStripper.VisibleLength(l) == 47);
    }

    [Fact]
    public void MinorSeparator_RendersHyphens()
    {
        var panel = new Panel
        {
            Width = 8,
            Sections = new[]
            {
                new Section { Rows = new Row[] { new EmptyRow() } },
                new Section { SeparatorAbove = RuleStyle.Minor, Rows = new Row[] { new EmptyRow() } }
            }
        };
        var lines = Lines(Renderer().Render(panel));
        lines.Select(TagStripper.StripTags).Should().Contain("|------|");
    }

    [Fact]
    public void MajorSeparator_RendersEquals()
    {
        var panel = new Panel
        {
            Width = 8,
            Sections = new[]
            {
                new Section { Rows = new Row[] { new EmptyRow() } },
                new Section { SeparatorAbove = RuleStyle.Major, Rows = new Row[] { new EmptyRow() } }
            }
        };
        var lines = Lines(Renderer().Render(panel));
        // Top border + major separator + bottom border = 3 lines of "|======|"
        lines.Count(l => TagStripper.StripTags(l) == "|======|").Should().Be(3);
    }

    [Fact]
    public void NoneSeparator_NoSeparatorLineEmitted()
    {
        var panel = new Panel
        {
            Width = 8,
            Sections = new[]
            {
                new Section { Rows = new Row[] { new EmptyRow() } },
                new Section { SeparatorAbove = RuleStyle.None, Rows = new Row[] { new EmptyRow() } }
            }
        };
        var lines = Lines(Renderer().Render(panel));
        // Only 2 major borders (top + bottom), no separator between sections
        lines.Count(l => TagStripper.StripTags(l) == "|======|").Should().Be(2);
        lines.Count(l => TagStripper.StripTags(l) == "|------|").Should().Be(0);
        lines.Count(l => TagStripper.StripTags(l) == "|      |").Should().Be(2); // 2 EmptyRows
    }

    [Fact]
    public void FirstSection_SeparatorAboveIsIgnored()
    {
        var panel = new Panel
        {
            Width = 8,
            Sections = new[]
            {
                // First section's SeparatorAbove must be ignored — top border is always Major
                new Section
                {
                    SeparatorAbove = RuleStyle.Minor,
                    Rows = new Row[] { new EmptyRow() }
                }
            }
        };
        var lines = Lines(Renderer().Render(panel));
        // Should have exactly: major border, empty row, major border — no minor rule
        lines.Select(TagStripper.StripTags).Should().NotContain("|------|");
        TagStripper.StripTags(lines[0]).Should().Be("|======|");
    }

    // ── EmptyRow ───────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyRow_FillsWithSpaces()
    {
        var panel = new Panel
        {
            Width = 8,
            Sections = new[]
            {
                new Section { Rows = new Row[] { new EmptyRow() } }
            }
        };
        var lines = Lines(Renderer().Render(panel));
        lines.Select(TagStripper.StripTags).Should().Contain("|      |");
    }

    [Fact]
    public void EmptyRow_Width80_Correct()
    {
        var panel = new Panel
        {
            Width = 80,
            Sections = new[] { new Section { Rows = new Row[] { new EmptyRow() } } }
        };
        var lines = Lines(Renderer().Render(panel));
        var emptyLine = lines.First(l => TagStripper.StripTags(l).StartsWith("| "));
        TagStripper.VisibleLength(emptyLine).Should().Be(80);
        TagStripper.StripTags(emptyLine).Should().Be("|" + new string(' ', 78) + "|");
    }

    // ── TextRow ────────────────────────────────────────────────────────────────

    [Fact]
    public void TextRow_LeftAlign_ContentFlushLeft()
    {
        var panel = new Panel
        {
            Width = 10,
            Sections = new[] { new Section { Rows = new Row[] {
                new TextRow { Content = "Hi" }
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        lines.Select(TagStripper.StripTags).Should().Contain("|Hi      |");  // 2 content + 6 spaces = 8 content area
    }

    [Fact]
    public void TextRow_RightAlign_ContentFlushRight()
    {
        var panel = new Panel
        {
            Width = 10,
            Sections = new[] { new Section { Rows = new Row[] {
                new TextRow { Content = "Hi", Align = Align.Right }
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        lines.Select(TagStripper.StripTags).Should().Contain("|      Hi|");
    }

    [Fact]
    public void TextRow_CenterAlign_ContentCentered_ExtraSpaceRight()
    {
        var panel = new Panel
        {
            Width = 12,
            Sections = new[] { new Section { Rows = new Row[] {
                new TextRow { Content = "Hi", Align = Align.Center }
            }}}
        };
        // content area = 10, text = 2, padding = 8, left = 4, right = 4
        var lines = Lines(Renderer().Render(panel));
        lines.Select(TagStripper.StripTags).Should().Contain("|    Hi    |");
    }

    [Fact]
    public void TextRow_CenterAlign_OddPadding_ExtraSpaceGoesRight()
    {
        var panel = new Panel
        {
            Width = 11,
            Sections = new[] { new Section { Rows = new Row[] {
                new TextRow { Content = "Hi", Align = Align.Center }
            }}}
        };
        // content area = 9, text = 2, padding = 7, left = 3, right = 4
        var lines = Lines(Renderer().Render(panel));
        lines.Select(TagStripper.StripTags).Should().Contain("|   Hi    |");
    }

    [Fact]
    public void TextRow_ContentExactWidth_NoTruncation()
    {
        var panel = new Panel
        {
            Width = 6,
            Sections = new[] { new Section { Rows = new Row[] {
                new TextRow { Content = "1234" }
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        lines.Select(TagStripper.StripTags).Should().Contain("|1234|");
    }

    [Fact]
    public void TextRow_ContentTooLong_TruncatesWithEllipsis()
    {
        // content area = 8, content = 12 visible → truncate to 5 + "..." = 8
        var panel = new Panel
        {
            Width = 10,
            Sections = new[] { new Section { Rows = new Row[] {
                new TextRow { Content = "Hello World!" }
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        lines.Select(TagStripper.StripTags).Should().Contain("|Hello...|");
    }

    [Fact]
    public void TextRow_ContentTooLong_Width4_NoEllipsis()
    {
        // content area = 2, content = 5 visible, width < 5 so no ellipsis — chop to 2
        var panel = new Panel
        {
            Width = 4,
            Sections = new[] { new Section { Rows = new Row[] {
                new TextRow { Content = "Hello" }
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        lines.Select(TagStripper.StripTags).Should().Contain("|He|");
    }

    [Fact]
    public void TextRow_LiteralAngleBrackets_CorrectWidth()
    {
        // Content with literal < and > must not be misread as tags — all chars count toward width
        var panel = new Panel
        {
            Width = 10,
            Sections = new[] { new Section { Rows = new Row[] {
                new TextRow { Content = "HP: <100>" }  // 9 chars, content area = 8 → truncated
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        TagStripper.VisibleLength(lines.First(l => l.Contains("HP:"))).Should().Be(10);
    }

    [Fact]
    public void TextRow_Wrap_BreaksOnWordBoundary()
    {
        var panel = new Panel
        {
            Width = 12,
            Sections = new[] { new Section { Rows = new Row[] {
                new TextRow { Content = "Hello World", Wrap = true }
            }}}
        };
        // content area = 10, "Hello World" (11) → wraps to "Hello" and "World"
        var lines = Lines(Renderer().Render(panel));
        lines.Select(TagStripper.StripTags).Should().Contain("|Hello     |");
        lines.Select(TagStripper.StripTags).Should().Contain("|World     |");
    }

    [Fact]
    public void TextRow_Wrap_False_Default_SingleLine()
    {
        var panel = new Panel
        {
            Width = 12,
            Sections = new[] { new Section { Rows = new Row[] {
                new TextRow { Content = "Hello World" }  // default Wrap = false
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        // content area = 10, "Hello World" (11) → truncated to "Hello W..." (10)
        lines.Select(TagStripper.StripTags).Should().Contain("|Hello W...|");
    }

    // ── TitleRow ───────────────────────────────────────────────────────────────

    [Fact]
    public void TitleRow_LeftOnly_AutoWrapsInTitleTag()
    {
        var panel = new Panel
        {
            Width = 20,
            Sections = new[] { new Section { Rows = new Row[] {
                new TitleRow { Left = "SCORE" }
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        var titleLine = lines.First(l => l.Contains("<title>SCORE</title>"));
        TagStripper.VisibleLength(titleLine).Should().Be(20);
    }

    [Fact]
    public void TitleRow_LeftAndRight_PlacedAtOppositeEnds()
    {
        var panel = new Panel
        {
            Width = 20,
            Sections = new[] { new Section { Rows = new Row[] {
                new TitleRow { Left = "PLAYER", Right = "Lv 12" }
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        var titleLine = lines.First(l => l.Contains("<title>PLAYER</title>"));
        titleLine.Should().Contain("<subtle>Lv 12</subtle>");
        TagStripper.VisibleLength(titleLine).Should().Be(20);
        titleLine.IndexOf("PLAYER").Should().BeLessThan(titleLine.IndexOf("Lv 12"));
    }

    [Fact]
    public void TitleRow_LeftAndRight_RightAlwaysPreserved_LeftTruncated()
    {
        // Width=12, borders=2, indent/padding=2, innerWidth=8, Left="VeryLongTitle" (13 vis), Right="R" (1 vis)
        // maxLeftVis = 8 - 1 = 7, ellipsisLen=3 (7>=5), truncate Left to 4 chars → "Very..."
        var panel = new Panel
        {
            Width = 12,
            Sections = new[] { new Section { Rows = new Row[] {
                new TitleRow { Left = "VeryLongTitle", Right = "R" }
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        var titleLine = lines.First(l => l.Contains("<subtle>R</subtle>"));
        TagStripper.VisibleLength(titleLine).Should().Be(12);
        titleLine.Should().Contain("<subtle>R</subtle>");
        TagStripper.StripTags(titleLine).Should().EndWith("R |");
        TagStripper.StripTags(titleLine).Should().StartWith("| Very...");
    }

    [Fact]
    public void TitleRow_LiteralAngleBrackets_CorrectWidth()
    {
        // Title content with literal < > must not skew padding.
        // Measure with theme-aware renderer so <frame>/<title> are stripped but <1d6> is counted as visible.
        var renderer = RendererWithTheme("frame", "title");
        var panel = new Panel
        {
            Width = 30,
            Sections = new[] { new Section { Rows = new Row[] {
                new TitleRow { Left = "dmg: <1d6>" }
            }}}
        };
        var lines = Lines(renderer.Render(panel));
        var titleLine = lines.First(l => l.Contains("dmg: <1d6>"));
        renderer.VisibleLength(titleLine).Should().Be(30);
    }

    // ── CellRow ────────────────────────────────────────────────────────────────

    [Fact]
    public void CellRow_TwoFixedCells_FillsWidth()
    {
        // Width=12, content area=10, two Fixed(5) cells, no dividers
        var panel = new Panel
        {
            Width = 12,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow
                {
                    Cells = new[]
                    {
                        new Cell { Content = "AB", Width = CellWidth.Fixed(5) },
                        new Cell { Content = "CD", Width = CellWidth.Fixed(5) }
                    }
                }
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        lines.Select(TagStripper.StripTags).Should().Contain("|AB   CD   |");
    }

    [Fact]
    public void CellRow_FillCell_AbsorbsRemainder()
    {
        // Width=12, content area=10, Fixed(4) + Fill — Fill gets 6
        var panel = new Panel
        {
            Width = 12,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow
                {
                    Cells = new[]
                    {
                        new Cell { Content = "AB", Width = CellWidth.Fixed(4) },
                        new Cell { Content = "CD", Width = CellWidth.Fill }
                    }
                }
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        lines.Select(TagStripper.StripTags).Should().Contain("|AB  CD    |");
    }

    [Fact]
    public void CellRow_TwoFillCells_SplitEvenly()
    {
        // Width=12, content area=10, two Fill cells each get 5
        var panel = new Panel
        {
            Width = 12,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow
                {
                    Cells = new[]
                    {
                        new Cell { Content = "A", Width = CellWidth.Fill },
                        new Cell { Content = "B", Width = CellWidth.Fill }
                    }
                }
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        lines.Select(TagStripper.StripTags).Should().Contain("|A    B    |");
    }

    [Fact]
    public void CellRow_TwoFillCells_OddRemainder_LastCellGetsExtra()
    {
        // Width=11, content area=9, two Fill cells: 9/2=4 each, remainder=1 goes to last
        var panel = new Panel
        {
            Width = 11,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow
                {
                    Cells = new[]
                    {
                        new Cell { Content = "A", Width = CellWidth.Fill },
                        new Cell { Content = "B", Width = CellWidth.Fill }
                    }
                }
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        // First fill = 4, last fill = 5
        lines.Select(TagStripper.StripTags).Should().Contain("|A   B    |");
    }

    [Fact]
    public void CellRow_ShowDividers_InsertsVerticalBars()
    {
        // Width=12, Fixed(4) + Fill, ShowDividers=true:
        // available = 10 - 1 = 9, fixed = 4, fill = 5
        // Output: |AAAA|B    |
        var panel = new Panel
        {
            Width = 12,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow
                {
                    ShowDividers = true,
                    Cells = new[]
                    {
                        new Cell { Content = "AAAA", Width = CellWidth.Fixed(4) },
                        new Cell { Content = "B", Width = CellWidth.Fill }
                    }
                }
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        lines.Select(TagStripper.StripTags).Should().Contain("|AAAA|B    |");
    }

    [Fact]
    public void CellRow_FixedSumExceedsAvailable_Throws()
    {
        // Width=8, content area=6, two Fixed(4) cells = 8 > 6 → throws
        var panel = new Panel
        {
            Width = 8,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow
                {
                    Cells = new[]
                    {
                        new Cell { Content = "A", Width = CellWidth.Fixed(4) },
                        new Cell { Content = "B", Width = CellWidth.Fixed(4) }
                    }
                }
            }}}
        };
        var act = () => Renderer().Render(panel);
        act.Should().Throw<InvalidOperationException>().WithMessage("*fixed widths*");
    }

    [Fact]
    public void CellRow_TruncatesLongContent_WithEllipsis()
    {
        // Fixed(8) cell, content = "Hello World" (11 chars)
        // truncate to 5 + "..." = 8 → "Hello..."
        var panel = new Panel
        {
            Width = 12,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow
                {
                    Cells = new[]
                    {
                        new Cell { Content = "Hello World", Width = CellWidth.Fixed(8) },
                        new Cell { Content = "B", Width = CellWidth.Fill }
                    }
                }
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        lines.Select(TagStripper.StripTags).Should().Contain("|Hello...B |");
    }

    [Fact]
    public void CellRow_LiteralAngleBrackets_CorrectWidth()
    {
        // Cell content with literal < > chars must not shrink the visible count.
        // Measure with theme-aware renderer so <frame> is stripped but <1d6> counts as visible.
        var renderer = RendererWithTheme("frame");
        var panel = new Panel
        {
            Width = 14,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow
                {
                    Cells = new[]
                    {
                        new Cell { Content = "dmg <1d6>", Width = CellWidth.Fixed(10) },
                        new Cell { Content = "B", Width = CellWidth.Fill }
                    }
                }
            }}}
        };
        var lines = Lines(renderer.Render(panel));
        var line = lines.First(l => l.Contains("dmg"));
        renderer.VisibleLength(line).Should().Be(14);
        line.Should().Contain("dmg <1d6>");
    }

    // ── ProgressCell ───────────────────────────────────────────────────────────

    [Fact]
    public void ProgressCell_Normal_RendersFilledAndEmptyChars()
    {
        // inner = 16, filled = Round(16 * 24800 / 32000, ToEven) = Round(12.4) = 12
        var panel = new Panel
        {
            Width = 80,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow { Cells = new Cell[] {
                    new ProgressCell { Value = 24800, Max = 32000, Width = CellWidth.Fixed(18) }
                }}
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        var barLine = lines.First(l => l.Contains("["));
        barLine.Should().Contain("[############----]");
    }

    [Fact]
    public void ProgressCell_ValueOverMax_ClampsBar()
    {
        // inner = 16, all filled when value >= max
        var panel = new Panel
        {
            Width = 80,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow { Cells = new Cell[] {
                    new ProgressCell { Value = 35200, Max = 32000, Width = CellWidth.Fixed(18) }
                }}
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        var barLine = lines.First(l => l.Contains("["));
        barLine.Should().Contain("[################]");
    }

    [Fact]
    public void ProgressCell_MaxZero_AllDashes()
    {
        var panel = new Panel
        {
            Width = 80,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow { Cells = new Cell[] {
                    new ProgressCell { Value = 0, Max = 0, Width = CellWidth.Fixed(18) }
                }}
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        var barLine = lines.First(l => l.Contains("["));
        barLine.Should().Contain("[----------------]");
    }

    [Fact]
    public void ProgressCell_NegativeValue_AllDashes()
    {
        var panel = new Panel
        {
            Width = 80,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow { Cells = new Cell[] {
                    new ProgressCell { Value = -1, Max = 100, Width = CellWidth.Fixed(18) }
                }}
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        var barLine = lines.First(l => l.Contains("["));
        barLine.Should().Contain("[----------------]");
    }

    [Fact]
    public void ProgressCell_FillWidth_FillsAvailableSpace()
    {
        // 80 - 2 borders - 16 label = 62 for fill progress cell, inner = 60
        var panel = new Panel
        {
            Width = 80,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow { Cells = new Cell[] {
                    new Cell { Content = "  Label         ", Width = CellWidth.Fixed(16) },
                    new ProgressCell { Value = 50, Max = 100, Width = CellWidth.Fill }
                }}
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        var barLine = lines.First(l => l.Contains("["));
        TagStripper.VisibleLength(barLine).Should().Be(80);
        barLine.Should().Contain("[");
        barLine.Should().Contain("]");
    }

    // ── FooterRow ──────────────────────────────────────────────────────────────

    [Fact]
    public void FooterRow_AutoWrapsInSubtleTag()
    {
        var panel = new Panel
        {
            Width = 40,
            Sections = new[] { new Section { Rows = new Row[] {
                new FooterRow { Content = "press enter" }
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        var footerLine = lines.First(l => l.Contains("press enter"));
        footerLine.Should().Contain("<subtle>press enter</subtle>");
        TagStripper.VisibleLength(footerLine).Should().Be(40);
    }

    // ── Panel-level invariants ─────────────────────────────────────────────────

    [Fact]
    public void SemanticTags_InStructuralOutput_NotConvertedToAnsi()
    {
        var panel = new Panel
        {
            Width = 30,
            Sections = new[] { new Section { Rows = new Row[] {
                new TitleRow { Left = "Stats" }
            }}}
        };
        var rendered = Renderer().Render(panel);
        rendered.Should().Contain("<title>Stats</title>");
        rendered.Should().NotContain("\x1b[");  // no ANSI escape codes
    }

    [Fact]
    public void MultiSection_AllThreeSeparatorTypes_CorrectLineCount()
    {
        var panel = new Panel
        {
            Width = 10,
            Sections = new[]
            {
                new Section { Rows = new Row[] { new EmptyRow() } },                               // S1
                new Section { SeparatorAbove = RuleStyle.Minor, Rows = new Row[] { new EmptyRow() } },  // S2
                new Section { SeparatorAbove = RuleStyle.None,  Rows = new Row[] { new EmptyRow() } },  // S3
                new Section { SeparatorAbove = RuleStyle.Major, Rows = new Row[] { new EmptyRow() } }   // S4
            }
        };
        var lines = Lines(Renderer().Render(panel));
        // top + S1row + minor + S2row + S3row + major + S4row + bottom = 8 lines
        lines.Should().HaveCount(8);
    }

    // ── Frame tags ────────────────────────────────────────────────────────────

    [Fact]
    public void MajorRule_WrappedInFrameTag()
    {
        var panel = new Panel { Width = 6, Sections = Array.Empty<Section>() };
        var lines = Lines(Renderer().Render(panel));
        lines[0].Should().Be("<frame>|====|</frame>");
        lines[1].Should().Be("<frame>|====|</frame>");
    }

    [Fact]
    public void MinorRule_WrappedInFrameTag()
    {
        var panel = new Panel
        {
            Width = 8,
            Sections = new[]
            {
                new Section { Rows = new Row[] { new EmptyRow() } },
                new Section { SeparatorAbove = RuleStyle.Minor, Rows = new Row[] { new EmptyRow() } }
            }
        };
        var lines = Lines(Renderer().Render(panel));
        lines.Should().Contain("<frame>|------|</frame>");
    }

    [Fact]
    public void ContentLine_BorderCharsWrappedInFrameTag()
    {
        var panel = new Panel
        {
            Width = 8,
            Sections = new[] { new Section { Rows = new Row[] { new EmptyRow() } } }
        };
        var lines = Lines(Renderer().Render(panel));
        // EmptyRow: <frame>|</frame>      <frame>|</frame>
        lines.Should().Contain(l =>
            l.StartsWith("<frame>|</frame>") && l.EndsWith("<frame>|</frame>"));
    }

    [Fact]
    public void CellRow_Divider_WrappedInFrameTag()
    {
        var panel = new Panel
        {
            Width = 12,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow
                {
                    ShowDividers = true,
                    Cells = new[]
                    {
                        new Cell { Content = "A", Width = CellWidth.Fixed(5) },
                        new Cell { Content = "B", Width = CellWidth.Fill }
                    }
                }
            }}}
        };
        var lines = Lines(Renderer().Render(panel));
        var cellLine = lines.First(l => l.Contains("A") && l.Contains("B"));
        // Both border and divider | chars wrapped in <frame>
        cellLine.Should().Contain("<frame>|</frame>");
    }

    [Fact]
    public void FrameTagsDoNotAffectVisibleLength()
    {
        var panel = new Panel
        {
            Width = 47,
            Sections = new[]
            {
                new Section { Rows = new Row[] { new EmptyRow(), new TextRow { Content = " Hello" } } }
            }
        };
        var lines = Lines(Renderer().Render(panel));
        lines.Should().OnlyContain(l => TagStripper.VisibleLength(l) == 47);
    }

    // ── Registered-tag content in cells (equipment command scenario) ───────────

    [Fact]
    public void CellRow_SubtleTaggedLabel_CorrectVisibleWidth()
    {
        // Equipment command passes <subtle>[Head]</subtle> as the label cell content.
        // Visible length of "[Head]" is 6, not 23 (with tags). Cell width=16, fill=62.
        // The tagged label should NOT trigger truncation — it fits in 16 visible chars.
        var renderer = RendererWithTheme("subtle", "frame");
        var panel = new Panel
        {
            Width = 80,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow
                {
                    Cells = new[]
                    {
                        new Cell { Content = "  <subtle>[Head]</subtle>", Width = CellWidth.Fixed(16) },
                        new Cell { Content = "a leather cap", Width = CellWidth.Fill }
                    }
                }
            }}}
        };
        var lines = Lines(renderer.Render(panel));
        var cellLine = lines.First(l => l.Contains("Head"));
        TagStripper.VisibleLength(cellLine).Should().Be(80);
        cellLine.Should().Contain("<subtle>[Head]</subtle>");
        cellLine.Should().Contain("a leather cap");
    }

    [Fact]
    public void CellRow_SubtleTaggedLabel_TruncatesAtVisibleBoundary()
    {
        // Cell width=10. Label "  <subtle>[LightningBolt]</subtle>" has 16 visible chars → truncated.
        // Must close the <subtle> tag at the cut point; must not leave a dangling open tag.
        var renderer = RendererWithTheme("subtle", "frame");
        var panel = new Panel
        {
            Width = 80,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow
                {
                    Cells = new[]
                    {
                        new Cell { Content = "  <subtle>[LightningBolt]</subtle>", Width = CellWidth.Fixed(10) },
                        new Cell { Content = "empty", Width = CellWidth.Fill }
                    }
                }
            }}}
        };
        var lines = Lines(renderer.Render(panel));
        var cellLine = lines.First(l => l.Contains("Lightn") || l.Contains("empty"));
        TagStripper.VisibleLength(cellLine).Should().Be(80);
        cellLine.Should().NotContain("[LightningBolt]");
        cellLine.Should().Contain("</subtle>");
    }

    [Fact]
    public void CellRow_SubtleEmptyItem_NoTagLeakage()
    {
        // Regression: <subtle>empty</subtle> in the fill cell must not bleed into adjacent rows.
        // Prior bug: unclosed <subtle> in label cell consumed the fill cell's </subtle>,
        // causing <frame>|</frame> border on adjacent lines to render as literal text.
        var renderer = RendererWithTheme("subtle", "frame");
        var panel = new Panel
        {
            Width = 80,
            Sections = new[] { new Section { Rows = new Row[] {
                new CellRow
                {
                    Cells = new[]
                    {
                        new Cell { Content = "  <subtle>[Head]</subtle>", Width = CellWidth.Fixed(16) },
                        new Cell { Content = "<subtle>empty</subtle>", Width = CellWidth.Fill }
                    }
                },
                new CellRow
                {
                    Cells = new[]
                    {
                        new Cell { Content = "  <subtle>[Neck]</subtle>", Width = CellWidth.Fixed(16) },
                        new Cell { Content = "<subtle>empty</subtle>", Width = CellWidth.Fill }
                    }
                }
            }}}
        };
        var rendered = renderer.Render(panel);
        var lines = Lines(rendered);
        // Every line must start with a frame border, never a literal tag mid-content
        lines.Should().OnlyContain(l =>
            l.StartsWith("<frame>|") || l.StartsWith("<frame>|="));
    }
}
