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

    // Author-declared { override: true } — routes this topic through RegistrationPolicy as an
    // override candidate (must declare a dependency edge on the owner of the topic it overrides).
    public bool Override { get; set; }

    // Author-declared { hidden: true } — suppresses the topic from listing surfaces (List/Categories).
    // GetTopicById still returns it; consumed by the listing filter in T6b.
    public bool Hidden { get; set; }

    [YamlIgnore]
    public string PackName { get; set; } = "";

    [YamlIgnore]
    public string NamespacedId => $"{PackName}:{Id}";
}
