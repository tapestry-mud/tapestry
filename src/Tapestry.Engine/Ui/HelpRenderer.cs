using System.Text;
using Tapestry.Shared.Help;

namespace Tapestry.Engine.Ui;

public static class HelpRenderer
{
    private const char Rule = '=';

    // Telnet requires CRLF. StringBuilder.AppendLine emits Environment.NewLine,
    // which is a bare LF in the Linux container and produces staggered output.
    private const string Crlf = "\r\n";

    public static string RenderTopic(HelpTopic topic, int width = 78)
    {
        var sb = new StringBuilder();
        var label = $" {topic.Title} ";
        var trailing = new string(Rule, Math.Max(0, width - 4 - label.Length));
        sb.Append($"  {new string(Rule, 3)}{label}{trailing}").Append(Crlf);
        sb.Append($"  <subtle>{topic.Brief}</subtle>").Append(Crlf);

        if (topic.Syntax.Count > 0)
        {
            sb.Append(Crlf);
            sb.Append("  Syntax:").Append(Crlf);
            foreach (var line in topic.Syntax)
            {
                sb.Append($"    {line}").Append(Crlf);
            }
        }

        sb.Append(Crlf);
        foreach (var line in topic.Body.Split('\n'))
        {
            sb.Append($"  {line.TrimEnd()}").Append(Crlf);
        }

        if (topic.SeeAlso.Count > 0)
        {
            sb.Append(Crlf);
            sb.Append($"  See also: {string.Join(", ", topic.SeeAlso)}").Append(Crlf);
        }

        sb.Append($"  {new string(Rule, width - 2)}").Append(Crlf);
        return sb.ToString();
    }

    public static string RenderDisambiguation(string term, List<HelpTopicSummary> matches, int width = 78)
    {
        var sb = new StringBuilder();
        var label = $" Help: \"{term}\" ";
        var trailing = new string(Rule, Math.Max(0, width - 4 - label.Length));
        sb.Append($"  {new string(Rule, 3)}{label}{trailing}").Append(Crlf);
        sb.Append("  Multiple matches found:").Append(Crlf);
        sb.Append(Crlf);

        var pad = matches.Count > 0 ? matches.Max(m => m.Id.Length) + 4 : 4;
        foreach (var m in matches)
        {
            sb.Append($"    {m.Id.PadRight(pad)}{m.Title}").Append(Crlf);
        }

        sb.Append(Crlf);
        sb.Append("  Type help [topic] for details.").Append(Crlf);
        sb.Append($"  {new string(Rule, width - 2)}").Append(Crlf);
        return sb.ToString();
    }

    public static string RenderNoMatch(string term)
    {
        return $"No help found for '{term}'.{Crlf}";
    }
}
