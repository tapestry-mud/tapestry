namespace Tapestry.Engine;

public readonly record struct ParsedCommand(string Verb, string[] Args);

public static class CommandInputParser
{
    private const int MaxChain = 20;

    public static IReadOnlyList<ParsedCommand> Parse(string rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput)) { return []; }

        var segments = rawInput.Split(';');
        var result = new List<ParsedCommand>(Math.Min(segments.Length, MaxChain));

        foreach (var segment in segments)
        {
            if (result.Count >= MaxChain) { break; }

            var trimmed = segment.Trim();
            if (string.IsNullOrEmpty(trimmed)) { continue; }

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) { continue; }

            var first = parts[0];
            var args = parts.Length > 1 ? parts[1..] : [];

            // Repeat expansion: "3n" expands to n n n; "2east" expands to east east
            // Note: command names starting with digits are conventionally forbidden in MUD usage,
            // so this expansion applies universally without command-name lookup.
            if (first.Length > 1 && char.IsDigit(first[0]))
            {
                var i = 0;
                while (i < first.Length && char.IsDigit(first[i])) { i++; }
                if (i > 0 && i < first.Length && int.TryParse(first[..i], out var count) && count >= 1)
                {
                    var verb = first[i..];
                    var expanded = Math.Min(count, MaxChain - result.Count);
                    for (var r = 0; r < expanded; r++)
                    {
                        result.Add(new ParsedCommand(verb, args));
                    }
                    continue;
                }
            }

            result.Add(new ParsedCommand(first, args));
        }

        return result;
    }
}
