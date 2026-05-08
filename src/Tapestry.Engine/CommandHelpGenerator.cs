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
            Keywords = [reg.Keyword]
        };
    }

    public static void GenerateAll(IEnumerable<CommandRegistration> registrations, HelpService helpService)
    {
        foreach (var reg in registrations)
        {
            var topic = GenerateFor(reg);
            if (topic != null)
            {
                helpService.AddTopic(topic, loadOrder: 0);  // pack help files (higher loadOrder) override
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
