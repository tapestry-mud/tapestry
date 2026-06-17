using Tapestry.Engine.Help;

namespace Tapestry.Engine;

public static class CommandHelpGenerator
{
    /// <summary>
    /// Stamp the args-derived syntax line onto each command-shaped authored topic.
    /// Runs at seal time so authors never hand-write syntax. Concept topics (no matching
    /// command) are untouched.
    /// </summary>
    public static void StampSyntax(IEnumerable<CommandRegistration> registrations, HelpService help)
    {
        foreach (var reg in registrations)
        {
            var topic = help.GetTopicById(reg.Keyword);
            if (topic == null) { continue; }
            topic.Syntax = new List<string> { BuildSyntax(reg) };
        }
    }

    public static string BuildSyntax(CommandRegistration reg)
    {
        var parts = new List<string> { reg.Keyword };

        if (reg.ArgDefinitions != null)
        {
            foreach (var (argName, def) in reg.ArgDefinitions)
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
        }

        return string.Join(" ", parts);
    }
}
