using System.Text;
using Tapestry.Engine.Color;

namespace Tapestry.Engine.Text;

/// <summary>
/// Word-wraps player-facing output to a visible column width, breaking only at
/// whitespace -- never hyphenating, never splitting a word (except a single token
/// longer than the whole width, which is hard-broken to honor the cap). Markup is
/// measured as zero-width so wrapping matches what the player actually sees: theme
/// tags (e.g. &lt;highlight&gt;..&lt;/highlight&gt;), literal &lt;color ...&gt; tags,
/// and {brace} color shorthand all occupy zero columns.
///
/// It operates on the *markup* string before <see cref="ColorRenderer"/> turns it into
/// ANSI, reusing the renderer's notion of zero-width markup (so the two cannot drift)
/// and never having to parse raw escape sequences. Physical lines are joined with CRLF
/// (the engine's telnet line convention). The whole wrapped string is rendered
/// downstream in a single pass, so a tag that spans an inserted line break still
/// renders correctly (terminals carry SGR state across the newline).
///
/// Lines that already fit are emitted verbatim, so pre-formatted content (panels, maps)
/// built within the width is untouched. Input is assumed strict 7-bit ASCII, so each
/// visible rune is exactly one column.
/// </summary>
public sealed class MarkupWrapper
{
    private readonly ThemeRegistry _theme;

    public MarkupWrapper(ThemeRegistry theme) => _theme = theme;

    /// <summary>Wraps <paramref name="text"/> to <paramref name="width"/> visible
    /// columns. A width &lt;= 0 disables wrapping (returns the text unchanged).</summary>
    public string Wrap(string text, int width)
    {
        if (width <= 0 || string.IsNullOrEmpty(text)) { return text; }

        var segments = text.Replace("\r\n", "\n").Split('\n');
        var lines = new List<string>();
        foreach (var segment in segments)
        {
            if (VisibleLength(segment) <= width)
            {
                lines.Add(segment);
            }
            else
            {
                WrapSegment(segment, width, lines);
            }
        }
        return string.Join("\r\n", lines);
    }

    /// <summary>Visible (on-screen) column count of <paramref name="s"/>, excluding
    /// zero-width markup. Equals <see cref="ColorRenderer.RenderPlain"/>'s length for
    /// well-formed markup.</summary>
    public int VisibleLength(string s)
    {
        var count = 0;
        var i = 0;
        while (i < s.Length)
        {
            if (IsZeroWidthMarkup(s, i, out var len))
            {
                i += len;
                continue;
            }
            count++;
            i++;
        }
        return count;
    }

    // Single forward pass: append zero-width markup verbatim; track the visible column
    // count and the position just after the most recent space (the break opportunity).
    // On overflow, break at that space (trimming it) and carry the partial word; if no
    // space has been seen, hard-break in place (an over-long single token).
    private void WrapSegment(string segment, int width, List<string> output)
    {
        var line = new StringBuilder();
        var lineVisible = 0;
        var breakPos = -1; // char index in `line` just after the most recent space

        var i = 0;
        while (i < segment.Length)
        {
            if (IsZeroWidthMarkup(segment, i, out var len))
            {
                line.Append(segment, i, len);
                i += len;
                continue;
            }

            var ch = segment[i];

            if (ch == ' ')
            {
                line.Append(' ');
                lineVisible++;
                breakPos = line.Length;
                i++;
                continue;
            }

            if (lineVisible >= width)
            {
                if (breakPos >= 0)
                {
                    var committed = line.ToString(0, breakPos).TrimEnd(' ');
                    var carry = line.ToString(breakPos, line.Length - breakPos);
                    output.Add(committed);
                    line.Clear();
                    line.Append(carry);
                    lineVisible = VisibleLength(carry);
                    breakPos = -1;
                }
                else
                {
                    output.Add(line.ToString());
                    line.Clear();
                    lineVisible = 0;
                    breakPos = -1;
                }
            }

            line.Append(ch);
            lineVisible++;
            i++;
        }

        output.Add(line.ToString().TrimEnd(' '));
    }

    // A markup token is zero-width iff ColorRenderer would render/strip it: a theme tag
    // or <color ...> (open or close), or a recognized {brace} color. Unknown
    // <tags>/{braces} are visible, matching the renderer's pass-through behavior.
    private bool IsZeroWidthMarkup(string s, int i, out int length)
    {
        length = 0;
        var c = s[i];
        if (c == '<')
        {
            var end = s.IndexOf('>', i);
            if (end != -1)
            {
                var inner = s[(i + 1)..end];
                var tagName = inner.TrimStart('/').Split(' ')[0];
                if (_theme.IsKnown(tagName) || tagName.Equals("color", StringComparison.OrdinalIgnoreCase))
                {
                    length = end - i + 1;
                    return true;
                }
            }
        }
        else if (c == '{')
        {
            var end = s.IndexOf('}', i + 1);
            if (end != -1)
            {
                var name = s[(i + 1)..end];
                if (ColorRenderer.IsBraceColor(name))
                {
                    length = end - i + 1;
                    return true;
                }
            }
        }
        return false;
    }
}
