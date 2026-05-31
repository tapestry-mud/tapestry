using System.Text.RegularExpressions;

namespace Tapestry.Authoring;

/// <summary>Cleans raw model output: removes chat-template special-token spans
/// (e.g. &lt;|im_start|&gt;) and trims surrounding whitespace.</summary>
public static partial class OutputSanitizer
{
    [GeneratedRegex(@"<\|[^>]*?\|>")]
    private static partial Regex SpecialTokenSpan();

    public static string Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }
        return SpecialTokenSpan().Replace(raw, "").Trim();
    }
}
