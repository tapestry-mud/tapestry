using System.Text.RegularExpressions;

namespace Tapestry.Scripting.Services;

public static class TextSanitizer
{
    // ANSI CSI sequences: ESC[ followed by parameter bytes and a final byte
    private static readonly Regex AnsiPattern =
        new(@"\x1B\[[0-9;]*[mGKHFJA-Z]", RegexOptions.Compiled);

    // Custom markup tags used by ApiMessaging and pack scripts:
    // <highlight>, <direction>, <npc>, <item.common>, <player>, <exits>, etc.
    private static readonly Regex MarkupPattern =
        new(@"<[^>]+>", RegexOptions.Compiled);

    public static string Strip(string text)
    {
        var noAnsi = AnsiPattern.Replace(text, "");
        return MarkupPattern.Replace(noAnsi, "");
    }
}
