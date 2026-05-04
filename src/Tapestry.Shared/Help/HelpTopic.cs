using YamlDotNet.Serialization;

namespace Tapestry.Shared.Help;

public class HelpTopic
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string Brief { get; set; } = "";
    public string Body { get; set; } = "";
    public List<string> Syntax { get; set; } = new();
    public List<string> Keywords { get; set; } = new();

    [YamlMember(Alias = "see_also")]
    public List<string> SeeAlso { get; set; } = new();

    public string? Role { get; set; }

    [YamlIgnore]
    public string PackName { get; set; } = "";

    [YamlIgnore]
    public string NamespacedId => $"{PackName}:{Id}";
}

public class HelpTopicSummary
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Brief { get; set; } = "";
}
