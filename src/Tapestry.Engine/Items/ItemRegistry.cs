namespace Tapestry.Engine.Items;

public class ItemRegistry
{
    private readonly Dictionary<string, ItemTemplate> _templates = new();

    public void Register(ItemTemplate template)
    {
        _templates[template.Id] = template;
    }

    public ItemTemplate? GetTemplate(string templateId)
    {
        return _templates.GetValueOrDefault(templateId);
    }

    public Entity? CreateItem(string templateId)
    {
        var template = GetTemplate(templateId);
        return template?.CreateEntity();
    }

    public bool HasTemplate(string templateId)
    {
        return _templates.ContainsKey(templateId);
    }

    public int Count => _templates.Count;
}
