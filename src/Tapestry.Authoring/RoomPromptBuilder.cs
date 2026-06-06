using System.Collections.Generic;
using System.Text;
using Tapestry.Engine.Authoring;

namespace Tapestry.Authoring;

/// <summary>Prompt text as data — overridable from config so tuning needs no recompile.
/// <paramref name="TaskLines"/> is keyed by lowercased field name.</summary>
public sealed record RecommendPromptConfig(
    string SystemPrompt,
    IReadOnlyDictionary<string, string> TaskLines,
    int MaxSentences,
    string NeighborGuidance = RecommendPromptConfig.DefaultNeighborGuidance)
{
    public const string DefaultSystemPrompt =
        "You are a terse MUD area author. Second person, present tense. No preamble, no 'Sure!'.";

    public const string DefaultNeighborGuidance =
        "Match the theme and tone of the adjacent rooms above, but make this room its own distinct place — do not copy their specific details or wording.";

    public static IReadOnlyDictionary<string, string> DefaultTaskLines => new Dictionary<string, string>
    {
        ["name"] = "Task: write a short, evocative room name (a few words). Output only the name.",
        ["description"] = "Task: write the room description. Output only the description.",
    };

    public IReadOnlyDictionary<string, string> AreaTaskLines { get; init; } = DefaultAreaTaskLines;

    public static IReadOnlyDictionary<string, string> DefaultAreaTaskLines => new Dictionary<string, string>
    {
        ["short"] = "Task: write a one-line blurb for this area (a single sentence). Output only the blurb.",
        ["description"] = "Task: write the player-facing area description. Output only the description.",
        ["theme"] = "Task: write a theme/mood brief that captures this area's atmosphere and motifs for an author (1-2 sentences). Output only the brief.",
        ["lore"] = "Task: write a short background/lore note for this area. Output only the note."
    };

    public static RecommendPromptConfig Default { get; } =
        new(DefaultSystemPrompt, DefaultTaskLines, MaxSentences: 2);
}

/// <summary>Assembles (RoomData + field + author hint) into a (system, user) prompt pair.</summary>
public sealed class RoomPromptBuilder
{
    private readonly RecommendPromptConfig _config;

    public RoomPromptBuilder(RecommendPromptConfig config)
    {
        _config = config;
    }

    public (string System, string User) Build(string field, RoomData ctx, string? hint)
    {
        var f = (field ?? "").ToLowerInvariant();
        var sb = new StringBuilder();

        // 1. Structural grounding. Prefer the area's display name + creative theme when known.
        if (!string.IsNullOrWhiteSpace(ctx.AreaName) && !string.IsNullOrWhiteSpace(ctx.AreaTheme))
        {
            sb.Append("Area: ").Append(ctx.AreaName).Append(". Theme: ").Append(ctx.AreaTheme).Append('\n');
        }
        else if (!string.IsNullOrWhiteSpace(ctx.AreaName))
        {
            sb.Append("Area: ").Append(ctx.AreaName).Append(".\n");
        }
        else if (!string.IsNullOrWhiteSpace(ctx.Area))
        {
            sb.Append("Area: ").Append(ctx.Area).Append(".\n");
        }
        if (!string.IsNullOrWhiteSpace(ctx.Biome))
        {
            sb.Append("Biome: ").Append(ctx.Biome).Append(".\n");
        }

        // 1b. Sibling fields: feed the room's OTHER authored field so e.g. `~` on `name`
        // with no hint can derive a name from the description just written, and vice versa.
        if (f == "name" && !string.IsNullOrWhiteSpace(ctx.Description))
        {
            sb.Append("Description: ").Append(ctx.Description).Append(".\n");
        }
        if (f == "description" && !string.IsNullOrWhiteSpace(ctx.Name))
        {
            sb.Append("Name: ").Append(ctx.Name).Append(".\n");
        }

        // 2. Exits, with target names resolved from neighbors where the room exists.
        if (ctx.Exits.Count > 0)
        {
            sb.Append("Exits: ").Append(FormatExits(ctx)).Append(".\n");
        }

        // 3. Neighbor flavor — name (+biome) plus a short description snippet so adjacency
        // propagates theme. Snippet is the neighbor's first sentence, capped, to keep the
        // prompt tight for a small local model and limit verbatim copying.
        var includedNeighborDescription = false;
        foreach (var n in ctx.Neighbors)
        {
            if (string.IsNullOrWhiteSpace(n.Name))
            {
                continue;
            }
            sb.Append("Nearby to the ").Append(n.Direction).Append(", \"").Append(n.Name).Append('"');
            if (!string.IsNullOrWhiteSpace(n.Biome))
            {
                sb.Append(" [").Append(n.Biome).Append(']');
            }
            var snippet = FirstSentence(n.Description, 160);
            if (!string.IsNullOrWhiteSpace(snippet))
            {
                sb.Append(": ").Append(snippet);
                includedNeighborDescription = true;
            }
            sb.Append(".\n");
        }
        if (includedNeighborDescription && !string.IsNullOrWhiteSpace(_config.NeighborGuidance))
        {
            sb.Append(_config.NeighborGuidance).Append('\n');
        }

        // 4. Author intent — placed prominently, when supplied.
        if (!string.IsNullOrWhiteSpace(hint))
        {
            sb.Append("Intent: ").Append(hint!.Trim()).Append(".\n");
        }

        // 5. Per-field task line (config-overridable; defaults from the LLooM-validated set).
        if (_config.TaskLines.TryGetValue(f, out var taskLine) && !string.IsNullOrWhiteSpace(taskLine))
        {
            sb.Append(taskLine).Append('\n');
        }

        // 6. Sentence constraint for description only.
        if (f == "description")
        {
            sb.Append("Constraint: <=").Append(_config.MaxSentences).Append(" sentences.\n");
        }

        return (_config.SystemPrompt, sb.ToString().TrimEnd('\n'));
    }

    /// <summary>First sentence of the text, trimmed and capped at <paramref name="maxChars"/>
    /// (ellipsis if truncated). Empty when the text is null/blank.</summary>
    private static string FirstSentence(string? text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }
        var t = text.Trim();
        var end = t.IndexOfAny(new[] { '.', '!', '?' });
        var sentence = end >= 0 ? t.Substring(0, end + 1) : t;
        if (sentence.Length > maxChars)
        {
            sentence = sentence.Substring(0, maxChars).TrimEnd() + "…";
        }
        return sentence;
    }

    private static string FormatExits(RoomData ctx)
    {
        var parts = new List<string>();
        foreach (var (dir, _) in ctx.Exits)
        {
            var name = ResolveNeighborName(ctx, dir);
            parts.Add(name != null ? $"{dir} -> \"{name}\"" : $"{dir} -> (unnamed)");
        }
        return string.Join(", ", parts);
    }

    private static string? ResolveNeighborName(RoomData ctx, string dir)
    {
        foreach (var n in ctx.Neighbors)
        {
            if (string.Equals(n.Direction, dir, System.StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(n.Name))
            {
                return n.Name;
            }
        }
        return null;
    }
}
