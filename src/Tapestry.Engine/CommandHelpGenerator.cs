using Tapestry.Engine.Help;
using Tapestry.Shared.Help;

namespace Tapestry.Engine;

public static class CommandHelpGenerator
{
    public static HelpTopic? GenerateFor(CommandRegistration reg)
    {
        if (reg.ArgDefinitions == null || reg.ArgDefinitions.Count == 0) { return null; }

        var syntax = BuildSyntax(reg);
        var rolesDisplay = string.Join(", ", reg.Roles.Select(r => r + "s"));

        return new HelpTopic
        {
            Id = reg.Keyword,
            Title = reg.Keyword,
            Category = string.IsNullOrEmpty(reg.Category) ? "commands" : reg.Category,
            Brief = reg.Description,
            Body = $"{reg.Description}\r\n\r\nCategory: {reg.Category}\r\nAvailable to: {rolesDisplay}",
            Syntax = [syntax],
            Keywords = [reg.Keyword],
            // Owned by the resolved command's pack so the generated topic never collides with,
            // and is never mistaken for, another pack's hand-authored help.
            PackName = reg.PackName
        };
    }

    /// <summary>
    /// Gap-filling: generate a topic only for command ids in <paramref name="registrations"/> that
    /// are NOT already covered by a winning hand-authored topic (<paramref name="coveredIds"/>).
    /// Runs after the seal so the registry is resolved and command ownership is final.
    /// </summary>
    public static void GenerateGaps(
        IEnumerable<CommandRegistration> registrations,
        ISet<string> coveredIds,
        HelpService helpService)
    {
        foreach (var reg in registrations)
        {
            if (coveredIds.Contains(reg.Keyword)) { continue; }
            var topic = GenerateFor(reg);
            if (topic != null)
            {
                helpService.AddTopic(topic);
            }
        }
    }

    private static string BuildSyntax(CommandRegistration reg)
    {
        var parts = new List<string> { reg.Keyword };

        foreach (var (argName, def) in reg.ArgDefinitions!)
        {
            foreach (var prep in def.Prepositions)
            {
                parts.Add(prep);
            }

            string placeholder = def.Bulk
                ? $"[{argName} | all | all.{argName}]"
                : $"[{argName}]";

            parts.Add(def.Required ? placeholder : $"({placeholder})");
        }

        return string.Join(" ", parts);
    }
}
