using Tapestry.Engine.Quests;

namespace Tapestry.Engine.Items;

public class ItemRegistry : IQuestItemRegistry
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

    public IEnumerable<ItemTemplate> AllTemplates => _templates.Values;

    public int Count => _templates.Count;

    /// <summary>Removes every template scoped to <paramref name="areaId"/>: ids matching
    /// "&lt;areaId&gt;:*" (minted loot plus the per-player starter-kit templates). The backing
    /// dictionary is case-SENSITIVE, so the comparison is Ordinal: an id in another case is a
    /// different template and is left alone. Returns the number removed; 0 is not an error.</summary>
    public int RemoveByArea(string areaId)
    {
        if (string.IsNullOrEmpty(areaId)) { return 0; }
        var prefix = areaId + ":";
        var doomed = _templates.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();
        foreach (var key in doomed) { _templates.Remove(key); }
        return doomed.Count;
    }
}
