using System.Text;
using Tapestry.Shared.Help;

namespace Tapestry.Engine.Ui;

public static class HelpRenderer
{
    private const char Rule = '━'; // ━

    public static string RenderTopic(HelpTopic topic, int width = 78)
    {
        var sb = new StringBuilder();
        var label = $" {topic.Title} ";
        var trailing = new string(Rule, Math.Max(0, width - 4 - label.Length));
        sb.AppendLine($"  {new string(Rule, 3)}{label}{trailing}");
        sb.AppendLine($"  <subtle>{topic.Brief}</subtle>");

        if (topic.Syntax.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  Syntax:");
            foreach (var line in topic.Syntax)
            {
                sb.AppendLine($"    {line}");
            }
        }

        sb.AppendLine();
        foreach (var line in topic.Body.Split('\n'))
        {
            sb.AppendLine($"  {line.TrimEnd()}");
        }

        if (topic.SeeAlso.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"  See also: {string.Join(", ", topic.SeeAlso)}");
        }

        sb.AppendLine($"  {new string(Rule, width - 2)}");
        return sb.ToString();
    }

    public static string RenderDisambiguation(string term, List<HelpTopicSummary> matches, int width = 78)
    {
        var sb = new StringBuilder();
        var label = $" Help: \"{term}\" ";
        var trailing = new string(Rule, Math.Max(0, width - 4 - label.Length));
        sb.AppendLine($"  {new string(Rule, 3)}{label}{trailing}");
        sb.AppendLine("  Multiple matches found:");
        sb.AppendLine();

        var pad = matches.Max(m => m.Id.Length) + 4;
        foreach (var m in matches)
        {
            sb.AppendLine($"    {m.Id.PadRight(pad)}{m.Title}");
        }

        sb.AppendLine();
        sb.AppendLine("  Type help [topic] for details.");
        sb.AppendLine($"  {new string(Rule, width - 2)}");
        return sb.ToString();
    }

    public static string RenderNoMatch(string term)
    {
        return $"No help found for '{term}'.\r\n";
    }
}
