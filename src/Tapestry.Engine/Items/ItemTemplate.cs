using Tapestry.Engine.Inventory;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Items;

public class ItemTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object?> Properties { get; set; } = new();
    public List<ModifierEntry> Modifiers { get; set; } = new();

    public Entity CreateEntity()
    {
        var entity = new Entity(Type, Name);

        foreach (var tag in Tags)
        {
            entity.AddTag(tag);
        }

        foreach (var (key, val) in Properties)
        {
            if (key == "room_id") { continue; }
            entity.SetProperty(key, val);
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

    public class ModifierEntry
    {
        public string Stat { get; set; } = "";
        public int Value { get; set; }
    }
}
