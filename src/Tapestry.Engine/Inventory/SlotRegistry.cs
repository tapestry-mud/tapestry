using System.Text.RegularExpressions;

namespace Tapestry.Engine.Inventory;

public class SlotRegistry
{
    private static readonly Regex SnakeCasePattern = new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);
    private readonly List<SlotDefinition> _slots = new();
    private readonly Dictionary<string, SlotDefinition> _byName = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterEngineSlot(string name, string display, int max)
    {
        ValidateSnakeCase(name);
        var slot = new SlotDefinition(name, display, max, "engine");
        _byName[name] = slot;
        if (!_slots.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            _slots.Add(slot);
        }
    }

    public void RegisterPackSlot(string packName, string name, string display, int max)
    {
        ValidateSnakeCase(name);
        var slot = new SlotDefinition(name, display, max, packName);
        _byName[name] = slot;
        if (!_slots.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            _slots.Add(slot);
        }
    }

    public void Register(SlotDefinition slot)
    {
        _byName[slot.Name] = slot;
        if (!_slots.Any(s => s.Name.Equals(slot.Name, StringComparison.OrdinalIgnoreCase)))
        {
            _slots.Add(slot);
        }
    }

    public SlotDefinition? GetSlot(string name)
    {
        return _byName.GetValueOrDefault(name);
    }

    public IReadOnlyList<SlotDefinition> AllSlots => _slots.AsReadOnly();

    private static void ValidateSnakeCase(string name)
    {
        if (name.Contains('-'))
        {
            throw new ArgumentException($"Slot name '{name}' contains hyphens. Use snake_case.", nameof(name));
        }
        if (!SnakeCasePattern.IsMatch(name))
        {
            throw new ArgumentException($"Slot name '{name}' must be snake_case.", nameof(name));
        }
    }
}
