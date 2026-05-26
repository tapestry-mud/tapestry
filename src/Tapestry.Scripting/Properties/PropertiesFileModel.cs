namespace Tapestry.Scripting.Properties;

public class PropertiesFileModel
{
    public Dictionary<string, PropertyEntryModel> Properties { get; set; } = new();
}

public class PropertyEntryModel
{
    public string Description { get; set; } = "";
    public string Type { get; set; } = "string";
    public List<string> AppliesTo { get; set; } = new();
    public double? Min { get; set; }
    public double? Max { get; set; }
    public List<string> Enum { get; set; } = new();
}
