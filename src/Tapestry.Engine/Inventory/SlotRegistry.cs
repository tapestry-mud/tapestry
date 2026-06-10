using System.Text.RegularExpressions;

namespace Tapestry.Engine.Inventory;

public class SlotRegistry
{
    private static readonly Regex SnakeCasePattern = new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    // Single source of truth: the name-keyed dictionary holds the definitions; the name list
    // only preserves insertion order. (Was: a dictionary that replaced on duplicates AND a
    // parallel list that kept the first -- GetSlot and AllSlots disagreed after a duplicate.)
    private readonly Dictionary<string, SlotDefinition> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _order = new();

    public void RegisterEngineSlot(string name, string display, int max)
    {
        ValidateSnakeCase(name);
        Add(new SlotDefinition(name, display, max, "engine"));
    }

    public void RegisterPackSlot(string packName, string name, string display, int max)
    {
        ValidateSnakeCase(name);
        Add(new SlotDefinition(name, display, max, packName));
    }

    public void Register(SlotDefinition slot)
    {
        Add(slot);
    }

    private void Add(SlotDefinition slot)
    {
        // Replace-in-place keeps the original order position; a new name appends.
        if (!_byName.ContainsKey(slot.Name))
        {
            _order.Add(slot.Name);
        }
        _byName[slot.Name] = slot;
    }

    public SlotDefinition? GetSlot(string name)
    {
        return _byName.GetValueOrDefault(name);
    }

    public IReadOnlyList<SlotDefinition> AllSlots =>
        _order.Select(name => _byName[name]).ToList().AsReadOnly();

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
