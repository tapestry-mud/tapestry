namespace Tapestry.Engine.Inventory;

public class StackEntry
{
    public string? TemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? RarityKey { get; set; }
    public string? EssenceKey { get; set; }
    public List<string> ItemIds { get; set; } = new();
}
