using System.Collections.Concurrent;
using System.Text;

namespace Tapestry.Engine.Color;

public class ColorRenderer
{
    private readonly ThemeRegistry _theme;
    private readonly ConcurrentDictionary<string, string> _ansiCache = new();
    private readonly ConcurrentDictionary<string, string> _plainCache = new();

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
        var i = 0;

        while (i < input.Length)
        {
            // Semantic or literal color tag
            if (input[i] == '<' && i + 1 < input.Length && input[i + 1] != '/')
            {
                var tagEnd = input.IndexOf('>', i);
                if (tagEnd == -1)
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
                    var closeTag = "</color>";
                    var closeIdx = input.IndexOf(closeTag, tagEnd, StringComparison.OrdinalIgnoreCase);
                    if (closeIdx == -1)
                    {
                        sb.Append(input[i]);
                        i++;
                        continue;
                    }

                    var innerText = input[(tagEnd + 1)..closeIdx];

                    if (!strip)
                    {
                        var ansi = ParseLiteralColor(tagContent);
                        sb.Append(ansi);
                        sb.Append(innerText);
                        sb.Append("\x1b[0m");
                    }
                    else
                    {
                        sb.Append(innerText);
                    }

                    i = closeIdx + closeTag.Length;
                    continue;
                }

                // Semantic tag — allowlist check
                var tagName = tagContent.Split(' ')[0];
                if (!_theme.IsKnown(tagName))
                {
                    // Unknown tag: pass through as literal text
                    sb.Append(input[i]);
                    i++;
                    continue;
                }

                var closeTagStr = $"</{tagName}>";
                var closeIndex = input.IndexOf(closeTagStr, tagEnd, StringComparison.OrdinalIgnoreCase);
                if (closeIndex == -1)
                {
                    sb.Append(input[i]);
                    i++;
                    continue;
                }

                var content = input[(tagEnd + 1)..closeIndex];

                if (!strip)
                {
                    var pair = _theme.Resolve(tagName);
                    if (pair != null)
                    {
                        sb.Append(pair.Open);
                        sb.Append(content);
                        sb.Append(pair.Close);
                    }
                    else
                    {
                        sb.Append(content);
                    }
                }
                else
                {
                    sb.Append(content);
                }

                i = closeIndex + closeTagStr.Length;
                continue;
            }

            // Closing tag on its own — only skip known tags; pass through unknown ones
            if (input[i] == '<' && i + 1 < input.Length && input[i + 1] == '/')
            {
                var endTag = input.IndexOf('>', i);
                if (endTag != -1)
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

        return sb.ToString();
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
