using Tapestry.Engine.Distribution;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Items;

public class ItemTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
    public Dictionary<string, object?> Properties { get; set; } = new();
    public List<ModifierEntry> Modifiers { get; set; } = new();
    public List<SpawnOnEntry> SpawnOn { get; set; } = new();

    public Entity CreateEntity()
    {
        var entity = new Entity(Type, Name);

        foreach (var tag in Tags)
        {
            if (!string.Equals(tag, entity.Type, StringComparison.OrdinalIgnoreCase))
            {
                entity.AddTag(tag);
            }
        }

        foreach (var keyword in Keywords)
        {
            entity.AddKeyword(keyword);
        }

        foreach (var (key, val) in Properties)
        {
            if (key == "room_id") { continue; }
            entity.SetProperty(key, NormalizeValue(val));
        }

        entity.SetProperty(CommonProperties.TemplateId, Id);

        var statModifiers = new List<StatModifier>();
        foreach (var mod in Modifiers)
        {
            if (Enum.TryParse<StatType>(mod.Stat, true, out var statType))
            {
                statModifiers.Add(new StatModifier($"equipment:{entity.Id}", statType, mod.Value));
            }
        }
        entity.SetProperty(InventoryProperties.Modifiers, statModifiers);

        return entity;
    }

    private static object? NormalizeValue(object? val)
    {
        if (val is Dictionary<object, object> untypedDict)
        {
            var normalized = new Dictionary<string, object?>();
            foreach (var kvp in untypedDict)
            {
                normalized[kvp.Key?.ToString() ?? ""] = NormalizeValue(kvp.Value);
            }
            return normalized;
        }
        return val;
    }

    public class ModifierEntry
    {
        public string Stat { get; set; } = "";
        public int Value { get; set; }
    }
}
