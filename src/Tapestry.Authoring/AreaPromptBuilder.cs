using System.Text;
using Tapestry.Engine.Authoring;

namespace Tapestry.Authoring;

/// <summary>Assembles (AreaData + field + author hint) into a (system, user) prompt pair.</summary>
public sealed class AreaPromptBuilder
{
    private readonly RecommendPromptConfig _config;

    public AreaPromptBuilder(RecommendPromptConfig config)
    {
        _config = config;
    }

    public (string System, string User) Build(string field, AreaData ctx, string? hint)
    {
        var f = (field ?? "").ToLowerInvariant();
        var sb = new StringBuilder();

        // 1. Structural grounding.
        sb.Append("Area: ").Append(ctx.Name).Append(".\n");
        if (ctx.LevelRange is { Length: 2 })
        {
            sb.Append("Level range: ").Append(ctx.LevelRange[0]).Append('-').Append(ctx.LevelRange[1]).Append(".\n");
        }

        // 2. Sibling-feed the area's other authored fields (skip the one being written).
        AppendSibling(sb, f, "short", "Short", ctx.Short);
        AppendSibling(sb, f, "description", "Description", ctx.Description);
        AppendSibling(sb, f, "theme", "Theme", ctx.Theme);
        AppendSibling(sb, f, "lore", "Lore", ctx.Lore);

        // 3. Member-room sample grounds suggestions in what the area contains.
        if (ctx.Rooms.Count > 0)
        {
            sb.Append("Rooms in this area:\n");
            foreach (var r in ctx.Rooms)
            {
                sb.Append("- ").Append(r.Name);
                if (!string.IsNullOrWhiteSpace(r.Biome))
                {
                    sb.Append(" (").Append(r.Biome).Append(')');
                }
                if (!string.IsNullOrWhiteSpace(r.DescriptionSnippet))
                {
                    sb.Append(": ").Append(r.DescriptionSnippet);
                }
                sb.Append('\n');
            }
        }

        // 4. Per-field task line.
        if (_config.AreaTaskLines.TryGetValue(f, out var taskLine) && !string.IsNullOrWhiteSpace(taskLine))
        {
            sb.Append(taskLine).Append('\n');
        }

        // 5. Optional author hint.
        if (!string.IsNullOrWhiteSpace(hint))
        {
            sb.Append("Author hint: ").Append(hint).Append('\n');
        }

        return (_config.SystemPrompt, sb.ToString());
    }

    private static void AppendSibling(StringBuilder sb, string current, string key, string label, string value)
    {
        if (current != key && !string.IsNullOrWhiteSpace(value))
        {
            sb.Append(label).Append(": ").Append(value).Append('\n');
        }
    }
}
