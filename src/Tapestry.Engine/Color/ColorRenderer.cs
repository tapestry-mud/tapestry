using System.Collections.Concurrent;
using System.Text;

namespace Tapestry.Engine.Color;

public class ColorRenderer
{
    private readonly ThemeRegistry _theme;
    private readonly ConcurrentDictionary<string, string> _ansiCache = new();
    private readonly ConcurrentDictionary<string, string> _plainCache = new();

    private static readonly Dictionary<string, string> BraceColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "\x1b[30m",
        ["red"] = "\x1b[31m",
        ["green"] = "\x1b[32m",
        ["yellow"] = "\x1b[33m",
        ["blue"] = "\x1b[34m",
        ["magenta"] = "\x1b[35m",
        ["cyan"] = "\x1b[36m",
        ["white"] = "\x1b[37m",
        ["bright_black"] = "\x1b[90m",
        ["bright_red"] = "\x1b[91m",
        ["bright_green"] = "\x1b[92m",
        ["bright_yellow"] = "\x1b[93m",
        ["bright_blue"] = "\x1b[94m",
        ["bright_magenta"] = "\x1b[95m",
        ["bright_cyan"] = "\x1b[96m",
        ["bright_white"] = "\x1b[97m",
        ["bold"] = "\x1b[1m",
        ["dim"] = "\x1b[2m",
        ["/"] = "\x1b[0m",
        ["reset"] = "\x1b[0m",
    };

    public ColorRenderer(ThemeRegistry theme)
    {
        _theme = theme;
    }

    public string RenderAnsi(string input)
    {
        if (string.IsNullOrEmpty(input)) { return input; }
        if (_ansiCache.TryGetValue(input, out var cached)) { return cached; }
        var result = RenderInternal(input, strip: false);
        _ansiCache.TryAdd(input, result);
        return result;
    }

    public string RenderPlain(string input)
    {
        if (string.IsNullOrEmpty(input)) { return input; }
        if (_plainCache.TryGetValue(input, out var cached)) { return cached; }
        var result = RenderInternal(input, strip: true);
        _plainCache.TryAdd(input, result);
        return result;
    }

    private string RenderInternal(string input, bool strip)
    {
        var sb = new StringBuilder(input.Length);
        // Stack of the ANSI open-codes for the currently-open ancestor tags.
        // When a tag closes we emit a reset then re-apply every ancestor still
        // on the stack, so the inner reset does not clear the parent's color.
        var styles = new Stack<string>();
        RenderSpan(input, 0, input.Length, strip, sb, styles);
        return sb.ToString();
    }

    // Renders input[start..end) into sb. `styles` holds the open-codes of the
    // enclosing tags (outermost at the bottom) so a close can restore them.
    private void RenderSpan(string input, int start, int end, bool strip, StringBuilder sb, Stack<string> styles)
    {
        var i = start;

        while (i < end)
        {
            // Semantic or literal color tag
            if (input[i] == '<' && i + 1 < end && input[i + 1] != '/')
            {
                var tagEnd = input.IndexOf('>', i);
                if (tagEnd == -1 || tagEnd >= end)
                {
                    sb.Append(input[i]);
                    i++;
                    continue;
                }

                var tagContent = input[(i + 1)..tagEnd];

                // Closing tag — skip it
                if (tagContent.StartsWith('/'))
                {
                    sb.Append(input[i]);
                    i++;
                    continue;
                }

                // Literal <color fg="..." bg="...">
                if (tagContent.StartsWith("color ", StringComparison.OrdinalIgnoreCase) ||
                    tagContent.Equals("color", StringComparison.OrdinalIgnoreCase))
                {
                    var closeIdx = FindMatchingClose(input, "color", tagEnd + 1, end);
                    if (closeIdx == -1)
                    {
                        sb.Append(input[i]);
                        i++;
                        continue;
                    }

                    var open = strip ? "" : ParseLiteralColor(tagContent);
                    AppendStyled(input, tagEnd + 1, closeIdx, open, strip, sb, styles);
                    i = closeIdx + "</color>".Length;
                    continue;
                }

                // Semantic tag — allowlist check
                var tagName = tagContent.Split(' ')[0];
                if (!_theme.IsKnown(tagName))
                {
                    // Unknown tag: pass through as literal text, keep scanning content
                    sb.Append(input[i]);
                    i++;
                    continue;
                }

                var closeIndex = FindMatchingClose(input, tagName, tagEnd + 1, end);
                if (closeIndex == -1)
                {
                    sb.Append(input[i]);
                    i++;
                    continue;
                }

                var pair = strip ? null : _theme.Resolve(tagName);
                AppendStyled(input, tagEnd + 1, closeIndex, pair?.Open ?? "", strip, sb, styles);
                i = closeIndex + ($"</{tagName}>").Length;
                continue;
            }

            // Brace color shorthand: {yellow}, {cyan}, {/}, etc.
            if (input[i] == '{')
            {
                var closeIdx = input.IndexOf('}', i + 1);
                if (closeIdx != -1 && closeIdx < end)
                {
                    var colorName = input[(i + 1)..closeIdx];
                    if (BraceColors.TryGetValue(colorName, out var code))
                    {
                        if (!strip) { sb.Append(code); }
                        i = closeIdx + 1;
                        continue;
                    }
                }
                sb.Append(input[i]);
                i++;
                continue;
            }

            // Closing tag on its own — only skip known tags; pass through unknown ones
            if (input[i] == '<' && i + 1 < end && input[i + 1] == '/')
            {
                var endTag = input.IndexOf('>', i);
                if (endTag != -1 && endTag < end)
                {
                    var closeName = input[(i + 2)..endTag];
                    if (_theme.IsKnown(closeName) || closeName.Equals("color", StringComparison.OrdinalIgnoreCase))
                    {
                        i = endTag + 1;
                        continue;
                    }
                }
            }

            sb.Append(input[i]);
            i++;
        }
    }

    // Renders the inner span input[contentStart..closeIndex) wrapped in `open`,
    // recursing so nested tags render, and restoring ancestor styles on close.
    private void AppendStyled(string input, int contentStart, int closeIndex, string open, bool strip, StringBuilder sb, Stack<string> styles)
    {
        if (!strip && open.Length > 0) { sb.Append(open); }

        styles.Push(open);
        RenderSpan(input, contentStart, closeIndex, strip, sb, styles);
        styles.Pop();

        if (!strip && open.Length > 0)
        {
            // Reset, then re-open every still-open ancestor so the parent color
            // (cleared by the reset) is restored for the text that follows.
            sb.Append("\x1b[0m");
            foreach (var ancestor in EnumerateOutermostFirst(styles))
            {
                sb.Append(ancestor);
            }
        }
    }

    // Finds the close tag matching the most recently opened `tagName`, honoring
    // same-name nesting via depth counting, within [searchStart, end).
    private static int FindMatchingClose(string input, string tagName, int searchStart, int end)
    {
        var openPrefix = $"<{tagName}";
        var closeTag = $"</{tagName}>";
        var depth = 0;
        var i = searchStart;

        while (i < end)
        {
            var nextClose = input.IndexOf(closeTag, i, StringComparison.OrdinalIgnoreCase);
            if (nextClose == -1 || nextClose >= end) { return -1; }

            // Count opens of the same tag before this close to balance nesting.
            var scan = i;
            while (true)
            {
                var nextOpen = input.IndexOf(openPrefix, scan, StringComparison.OrdinalIgnoreCase);
                if (nextOpen == -1 || nextOpen >= nextClose) { break; }
                // Confirm it is a real open tag (`<tag>` or `<tag ...>`), not a prefix collision.
                var after = nextOpen + openPrefix.Length;
                if (after < end && (input[after] == '>' || input[after] == ' '))
                {
                    depth++;
                }
                scan = nextOpen + openPrefix.Length;
            }

            if (depth == 0) { return nextClose; }
            depth--;
            i = nextClose + closeTag.Length;
        }

        return -1;
    }

    private static IEnumerable<string> EnumerateOutermostFirst(Stack<string> styles)
    {
        // Stack enumerates top-first; reverse to emit outermost ancestor first.
        var buffer = new List<string>(styles.Count);
        foreach (var s in styles)
        {
            if (s.Length > 0) { buffer.Add(s); }
        }
        buffer.Reverse();
        return buffer;
    }

    private static string ParseLiteralColor(string tagContent)
    {
        var sb = new StringBuilder();

        var fgStart = tagContent.IndexOf("fg=\"", StringComparison.OrdinalIgnoreCase);
        if (fgStart != -1)
        {
            fgStart += 4;
            var fgEnd = tagContent.IndexOf('"', fgStart);
            if (fgEnd != -1)
            {
                var colorName = tagContent[fgStart..fgEnd];
                var code = ThemeRegistry.ResolveFgColor(colorName);
                if (code != null) { sb.Append(code); }
            }
        }

        var bgStart = tagContent.IndexOf("bg=\"", StringComparison.OrdinalIgnoreCase);
        if (bgStart != -1)
        {
            bgStart += 4;
            var bgEnd = tagContent.IndexOf('"', bgStart);
            if (bgEnd != -1)
            {
                var colorName = tagContent[bgStart..bgEnd];
                var code = ThemeRegistry.ResolveBgColor(colorName);
                if (code != null) { sb.Append(code); }
            }
        }

        return sb.ToString();
    }
}
