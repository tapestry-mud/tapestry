using Tapestry.Engine.Items;

namespace Tapestry.Engine.Inventory;

public class StackingService
{
    private readonly List<string> _extraKeys = new();

    public void AddKey(string propertyName)
    {
        if (!_extraKeys.Contains(propertyName, StringComparer.OrdinalIgnoreCase))
        {
            _extraKeys.Add(propertyName);
        }
    }

    public List<StackEntry> GetStacks(Entity entity)
    {
        var orderedKeys = new List<string>();
        var groups = new Dictionary<string, StackEntry>(StringComparer.Ordinal);

        foreach (var item in entity.Contents)
        {
            var templateId = item.GetProperty<string>("template_id");
            string stackKey;

            if (templateId == null)
            {
                stackKey = $"notemplate:{item.Id}";
            }
            else
            {
                var essence = item.GetProperty<string>(ItemProperties.Essence) ?? string.Empty;
                stackKey = $"{templateId}|{essence}";
                foreach (var extraKey in _extraKeys)
                {
                    stackKey += $"|{item.GetProperty<string>(extraKey) ?? string.Empty}";
                }
            }

            if (groups.TryGetValue(stackKey, out var existing))
            {
                existing.Quantity++;
                existing.ItemIds.Add(item.Id.ToString());
            }
            else
            {
                orderedKeys.Add(stackKey);
                var essenceVal = item.GetProperty<string>(ItemProperties.Essence);
                groups[stackKey] = new StackEntry
                {
                    TemplateId = templateId,
                    Name = item.Name,
                    Quantity = 1,
                    RarityKey = item.GetProperty<string>(ItemProperties.Rarity),
                    EssenceKey = string.IsNullOrEmpty(essenceVal) ? null : essenceVal,
                    ItemIds = new List<string> { item.Id.ToString() }
                };
            }
        }

        return orderedKeys.Select(k => groups[k]).ToList();
    }
}
