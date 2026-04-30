using System.Text;
using Tapestry.Engine.Color;

namespace Tapestry.Engine.Ui;

public class PanelRenderer
{
    private readonly ThemeRegistry? _theme;

    public PanelRenderer(ThemeRegistry theme) => _theme = theme;
    public PanelRenderer() { }

    public string Render(Panel panel)
    {
        var lines = new List<string>();
        lines.Add(MajorRule(panel.Width));

        for (var si = 0; si < panel.Sections.Count; si++)
        {
            var section = panel.Sections[si];
            if (si > 0)
            {
                switch (section.SeparatorAbove)
                {
                    case RuleStyle.Major:
                    {
                        lines.Add(MajorRule(panel.Width));
                        break;
                    }
                    case RuleStyle.Minor:
                    {
                        lines.Add(MinorRule(panel.Width));
                        break;
                    }
                    default:
                    {
                        // RuleStyle.None: no separator — intentional no-op
                        break;
                    }
                }
            }
            foreach (var row in section.Rows)
            {
                lines.AddRange(RenderRow(row, panel.Width));
            }
        }

        lines.Add(MajorRule(panel.Width));
        return string.Join("\r\n", lines);
    }

    private const string B = "<frame>|</frame>";

    private static string MajorRule(int width) => $"<frame>|{new string('=', width - 2)}|</frame>";
    private static string MinorRule(int width) => $"<frame>|{new string('-', width - 2)}|</frame>";

    private IEnumerable<string> RenderRow(Row row, int width)
    {
        return row switch
        {
            EmptyRow => RenderEmptyRow(width),
            TitleRow tr => RenderTitleRow(tr, width),
            TextRow tx => RenderTextRow(tx, width),
            CellRow cr => RenderCellRow(cr, width),
            FooterRow fr => RenderFooterRow(fr, width),
            _ => throw new NotImplementedException($"No renderer for row type {row.GetType().Name}")
        };
    }

    private static IEnumerable<string> RenderEmptyRow(int width)
    {
        yield return B + new string(' ', width - 2) + B;
    }

    private static IEnumerable<string> RenderTitleRow(TitleRow row, int width)
    {
        var contentWidth = width - 2;
        var innerWidth = contentWidth - 2;
        var left = row.Left;
        var leftVis = left.Length;

        if (row.Right == null)
        {
            yield return B + " " + PadToVisible($"<title>{left}</title>", innerWidth, leftVis) + " " + B;
            yield break;
        }

        var right = row.Right;
        var rightVis = right.Length;

        if (rightVis >= innerWidth)
        {
            throw new InvalidOperationException(
                $"TitleRow Right visible length ({rightVis}) equals or exceeds inner width ({innerWidth}). Shorten the Right value or increase panel width.");
        }

        if (leftVis + rightVis > innerWidth)
        {
            var maxLeftVis = innerWidth - rightVis;
            var ellipsisLen = maxLeftVis >= 5 ? 3 : 0;
            left = left.Substring(0, maxLeftVis - ellipsisLen);
            if (ellipsisLen > 0) { left += "..."; }
            leftVis = left.Length;
        }

        var gap = innerWidth - leftVis - rightVis;
        yield return B + " " + $"<title>{left}</title>" + new string(' ', gap) + $"<subtle>{right}</subtle>" + " " + B;
    }

    private IEnumerable<string> RenderTextRow(TextRow row, int width)
    {
        var contentWidth = width - 2;

        if (!row.Wrap)
        {
            yield return B + RenderInWidth(row.Content, contentWidth, row.Align) + B;
            yield break;
        }

        foreach (var segment in WrapContent(row.Content, contentWidth))
        {
            yield return B + RenderInWidth(segment, contentWidth, row.Align) + B;
        }
    }

    private IEnumerable<string> RenderCellRow(CellRow row, int width)
    {
        var contentWidth = width - 2;
        var dividerCount = row.ShowDividers ? row.Cells.Count - 1 : 0;
        var available = contentWidth - dividerCount;

        var fixedSum = row.Cells.Sum(c => c.Width.IsFill ? 0 : c.Width.Value);
        if (fixedSum > available)
        {
            throw new InvalidOperationException(
                $"CellRow fixed widths ({fixedSum}) exceed available content width ({available}).");
        }

        var fillCount = row.Cells.Count(c => c.Width.IsFill);
        var fillEach = fillCount > 0 ? (available - fixedSum) / fillCount : 0;
        var fillRemainder = fillCount > 0 ? (available - fixedSum) % fillCount : 0;

        var cellWidths = new int[row.Cells.Count];
        var fillsSeen = 0;
        for (var i = 0; i < row.Cells.Count; i++)
        {
            if (row.Cells[i].Width.IsFill)
            {
                fillsSeen++;
                cellWidths[i] = fillsSeen == fillCount ? fillEach + fillRemainder : fillEach;
            }
            else
            {
                cellWidths[i] = row.Cells[i].Width.Value;
            }
        }

        var sb = new StringBuilder(B);
        for (var i = 0; i < row.Cells.Count; i++)
        {
            if (row.ShowDividers && i > 0) { sb.Append(B); }
            if (row.Cells[i] is ProgressCell pc)
            {
                sb.Append(RenderProgressBar(pc, cellWidths[i]));
            }
            else
            {
                sb.Append(RenderInWidth(row.Cells[i].Content, cellWidths[i], row.Cells[i].Align));
            }
        }
        sb.Append(B);

        yield return sb.ToString();
    }

    private static string RenderProgressBar(ProgressCell cell, int barWidth)
    {
        var inner = barWidth - 2;
        if (inner <= 0 || cell.Max <= 0 || cell.Value < 0)
        {
            return "[" + new string('-', Math.Max(0, inner)) + "]";
        }
        var clamped = Math.Min(cell.Value, cell.Max);
        var filled = (int)Math.Round((double)inner * clamped / cell.Max, MidpointRounding.ToEven);
        var empty = inner - filled;
        return "[" + new string('#', filled) + new string('-', empty) + "]";
    }

    private static IEnumerable<string> RenderFooterRow(FooterRow row, int width)
    {
        var footerVis = row.Content.Length;
        var content = $"<subtle>{row.Content}</subtle>";
        yield return B + PadToVisible(content, width - 2, footerVis) + B;
    }

    // ── Width helpers ──────────────────────────────────────────────────────────

    /// Pads content to targetWidth visible chars. visLen is the pre-computed visible length of content.
    internal static string PadToVisible(string content, int targetWidth, int visLen)
    {
        return visLen >= targetWidth ? content : content + new string(' ', targetWidth - visLen);
    }

    /// Renders content into exactly targetWidth visible chars: truncate (with ...) or pad.
    /// Tag-aware: registered markup tags are excluded from visible length calculation.
    internal string RenderInWidth(string content, int targetWidth, Align align = Align.Left)
    {
        var visLen = VisibleLength(content);

        if (visLen > targetWidth)
        {
            var ellipsisLen = targetWidth >= 5 ? 3 : 0;
            content = TruncateToVisible(content, targetWidth - ellipsisLen);
            if (ellipsisLen > 0) { content += "..."; }
            visLen = VisibleLength(content);
        }

        var padding = Math.Max(0, targetWidth - visLen);

        return align switch
        {
            Align.Right => new string(' ', padding) + content,
            Align.Center => new string(' ', padding / 2) + content + new string(' ', padding - padding / 2),
            _ => content + new string(' ', padding)   // Left
        };
    }

    /// Returns the visible character count of a string, skipping registered markup tags.
    /// Unregistered angle-bracket sequences (e.g. <1d6>) are counted as visible chars.
    public int VisibleLength(string s)
    {
        var count = 0;
        var i = 0;

        while (i < s.Length)
        {
            if (s[i] == '<')
            {
                var end = s.IndexOf('>', i);
                if (end != -1)
                {
                    var tagName = s[(i + 1)..end].TrimStart('/').Split(' ')[0];
                    if (IsKnownTag(tagName))
                    {
                        i = end + 1;
                        continue;
                    }
                }
            }

            count++;
            i++;
        }

        return count;
    }

    /// Truncates content to maxVisLen visible characters, properly closing any open registered tags.
    public string TruncateToVisible(string content, int maxVisLen)
    {
        var sb = new StringBuilder();
        var visCount = 0;
        var i = 0;
        var openTags = new Stack<string>();

        while (i < content.Length && visCount < maxVisLen)
        {
            if (content[i] == '<')
            {
                var end = content.IndexOf('>', i);
                if (end != -1)
                {
                    var inner = content[(i + 1)..end];
                    var isClose = inner.StartsWith('/');
                    var tagName = inner.TrimStart('/').Split(' ')[0];

                    if (IsKnownTag(tagName))
                    {
                        if (isClose)
                        {
                            if (openTags.Count > 0) { openTags.Pop(); }
                        }
                        else
                        {
                            openTags.Push(tagName);
                        }

                        sb.Append(content[i..(end + 1)]);
                        i = end + 1;
                        continue;
                    }

                    // Unregistered <tag> with matching > — visible sequence; treat as atomic unit.
                    // Include it whole if it fits, otherwise stop before the < to avoid a dangling open bracket.
                    var seqLen = end - i + 1;
                    if (visCount + seqLen <= maxVisLen)
                    {
                        sb.Append(content[i..(end + 1)]);
                        visCount += seqLen;
                        i = end + 1;
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            sb.Append(content[i]);
            visCount++;
            i++;
        }

        while (openTags.Count > 0)
        {
            sb.Append($"</{openTags.Pop()}>");
        }

        return sb.ToString();
    }

    private bool IsKnownTag(string tagName) =>
        _theme?.IsKnown(tagName) == true ||
        tagName.Equals("color", StringComparison.OrdinalIgnoreCase);

    /// Word-wraps plain-text content into segments of at most targetWidth chars.
    internal static IEnumerable<string> WrapContent(string content, int targetWidth)
    {
        if (content.Length == 0) { yield break; }

        var words = content.Split(' ');
        var lineWords = new List<string>();
        var lineLen = 0;

        foreach (var word in words)
        {
            if (word.Length == 0) { continue; }

            if (lineLen == 0)
            {
                lineWords.Add(word);
                lineLen = word.Length;
            }
            else if (lineLen + 1 + word.Length <= targetWidth)
            {
                lineWords.Add(word);
                lineLen += 1 + word.Length;
            }
            else
            {
                yield return string.Join(' ', lineWords);
                lineWords.Clear();
                lineWords.Add(word);
                lineLen = word.Length;
            }
        }

        if (lineWords.Count > 0) { yield return string.Join(' ', lineWords); }
    }
}
