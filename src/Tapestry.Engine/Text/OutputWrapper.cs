using System.Text;

namespace Tapestry.Engine.Text;

/// <summary>
/// Word-wraps already-rendered player output to a visible column width, breaking only at
/// whitespace -- never hyphenating, never splitting a word. ANSI SGR/CSI escape sequences
/// (ESC '[' ... final-byte) are copied verbatim and counted as ZERO visible columns, so
/// wrapping reflects exactly what the terminal shows.
///
/// It runs as the INNER decorator, AFTER <see cref="Color.ColorRenderingConnection"/>:
/// by the time text reaches it, color tags have already been rendered to escapes (ANSI
/// clients) or stripped (plain clients), so there is no markup left to misjudge. Malformed,
/// nested, or unclosed source markup has already been resolved to literal visible text by
/// the renderer and is therefore measured correctly -- the width cap cannot be defeated by
/// markup. (The renderer also caches the pre-wrap string, so wrapping does not fragment its
/// cache across widths.)
///
/// A single token longer than the whole width OVERFLOWS its line intact rather than being
/// split -- honoring "never break a word" -- so such a line may exceed the cap by design.
/// Physical lines join with CRLF; an inserted newline sits inside any active color span and
/// terminals carry SGR state across it. Lines that already fit are emitted verbatim, so
/// pre-formatted content built within the width is untouched. Input is strict 7-bit ASCII
/// (plus ESC for SGR), so every visible byte is one column.
/// </summary>
public sealed class OutputWrapper
{
    /// <summary>Wraps <paramref name="text"/> to <paramref name="width"/> visible columns.
    /// A width &lt;= 0 disables wrapping (returns the text unchanged).</summary>
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

    /// <summary>Visible (on-screen) column count of <paramref name="s"/>, excluding ANSI
    /// escape sequences.</summary>
    public int VisibleLength(string s)
    {
        var count = 0;
        var i = 0;
        while (i < s.Length)
        {
            if (IsEscape(s, i, out var len))
            {
                i += len;
                continue;
            }
            count++;
            i++;
        }
        return count;
    }

    // Single forward pass: copy escapes verbatim; track the visible column count and the
    // position just after the most recent space (the break opportunity). On overflow, break
    // at that space (trimming it) and carry the partial word. With NO space seen, keep
    // appending -- an over-long token overflows whole rather than being split mid-word.
    private void WrapSegment(string segment, int width, List<string> output)
    {
        var line = new StringBuilder();
        var lineVisible = 0;
        var breakPos = -1; // char index in `line` just after the most recent space

        var i = 0;
        while (i < segment.Length)
        {
            if (IsEscape(segment, i, out var len))
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

            if (lineVisible >= width && breakPos >= 0)
            {
                var committed = line.ToString(0, breakPos).TrimEnd(' ');
                var carry = line.ToString(breakPos, line.Length - breakPos);
                output.Add(committed);
                line.Clear();
                line.Append(carry);
                lineVisible = VisibleLength(carry);
                breakPos = -1;
            }

            line.Append(ch);
            lineVisible++;
            i++;
        }

        output.Add(line.ToString().TrimEnd(' '));
    }

    // An ANSI CSI escape: ESC '[' (params/intermediates 0x20-0x3F)* (final byte 0x40-0x7E).
    // Returns its full length; counted as zero visible columns. A lone ESC (no '[') is not
    // treated as an escape and counts as a visible char (the renderer never emits one).
    private static bool IsEscape(string s, int i, out int length)
    {
        length = 0;
        if (s[i] != '\x1b' || i + 1 >= s.Length || s[i + 1] != '[') { return false; }
        var j = i + 2;
        while (j < s.Length && s[j] >= (char)0x20 && s[j] <= (char)0x3F) { j++; }
        if (j < s.Length && s[j] >= (char)0x40 && s[j] <= (char)0x7E) { j++; }
        length = j - i;
        return true;
    }
}
