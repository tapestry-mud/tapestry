namespace Tapestry.Engine.Color;

public class ThemeRegistry
{
    private readonly Dictionary<string, ThemeEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, AnsiPair> _compiled = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> FgCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "\x1b[30m",
        ["red"] = "\x1b[31m",
        ["green"] = "\x1b[32m",
        ["yellow"] = "\x1b[33m",
        ["blue"] = "\x1b[34m",
        ["magenta"] = "\x1b[35m",
        ["cyan"] = "\x1b[36m",
        ["white"] = "\x1b[37m",
        ["bright-black"] = "\x1b[90m",
        ["dark-gray"] = "\x1b[90m",
        ["bright-red"] = "\x1b[91m",
        ["bright-green"] = "\x1b[92m",
        ["bright-yellow"] = "\x1b[93m",
        ["bright-blue"] = "\x1b[94m",
        ["bright-magenta"] = "\x1b[95m",
        ["bright-cyan"] = "\x1b[96m",
        ["bright-white"] = "\x1b[97m"
    };

    private static readonly Dictionary<string, string> BgCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "\x1b[40m",
        ["red"] = "\x1b[41m",
        ["green"] = "\x1b[42m",
        ["yellow"] = "\x1b[43m",
        ["blue"] = "\x1b[44m",
        ["magenta"] = "\x1b[45m",
        ["cyan"] = "\x1b[46m",
        ["white"] = "\x1b[47m",
        ["bright-black"] = "\x1b[100m",
        ["bright-red"] = "\x1b[101m",
        ["bright-green"] = "\x1b[102m",
        ["bright-yellow"] = "\x1b[103m",
        ["bright-blue"] = "\x1b[104m",
        ["bright-magenta"] = "\x1b[105m",
        ["bright-cyan"] = "\x1b[106m",
        ["bright-white"] = "\x1b[107m"
    };

    public void Register(string tag, ThemeEntry entry)
    {
        _entries[tag] = entry;
    }

    public void Compile()
    {
        _compiled = new Dictionary<string, AnsiPair>(StringComparer.OrdinalIgnoreCase);

        foreach (var (tag, entry) in _entries)
        {
            var open = "";
            if (entry.Fg != null && FgCodes.TryGetValue(entry.Fg, out var fgCode))
            {
                open += fgCode;
            }
            if (entry.Bg != null && BgCodes.TryGetValue(entry.Bg, out var bgCode))
            {
                open += bgCode;
            }

            if (!string.IsNullOrEmpty(open))
            {
                _compiled[tag] = new AnsiPair(open, "\x1b[0m");
            }
        }
    }

    public AnsiPair? Resolve(string tag)
    {
        return _compiled.GetValueOrDefault(tag);
    }

    public bool IsKnown(string tag)
    {
        return _entries.ContainsKey(tag);
    }

    public IReadOnlyDictionary<string, string> GetHtmlMap()
    {
        return _entries
            .Where(kvp => kvp.Value.Html != null)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Html!,
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Resolves a color name to its ANSI fg code (for literal color tags).</summary>
    public static string? ResolveFgColor(string colorName)
    {
        return FgCodes.GetValueOrDefault(colorName);
    }

    public static string? ResolveBgColor(string colorName)
    {
        return BgCodes.GetValueOrDefault(colorName);
    }
}
