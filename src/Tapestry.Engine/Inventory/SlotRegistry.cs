namespace Tapestry.Engine.Inventory;

public class SlotRegistry
{
    private readonly List<SlotDefinition> _slots = new();
    private readonly Dictionary<string, SlotDefinition> _byName = new(StringComparer.OrdinalIgnoreCase);

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
}
