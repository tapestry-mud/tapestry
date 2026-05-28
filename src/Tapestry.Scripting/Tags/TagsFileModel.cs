namespace Tapestry.Scripting.Tags;

public class TagsFileModel
{
    public Dictionary<string, TagEntryModel> Tags { get; set; } = new();
}

public class TagEntryModel
{
    public string Description { get; set; } = "";
    public List<string> AppliesTo { get; set; } = new();
    public string? Kind { get; set; }
}
