namespace Tapestry.Engine.Abilities;

public class AbilityRegistry
{
    private readonly Dictionary<string, AbilityDefinition> _abilities = new(StringComparer.OrdinalIgnoreCase);

    public void Register(AbilityDefinition definition)
    {
        if (_abilities.TryGetValue(definition.Id, out var existing))
        {
            if (definition.Priority > existing.Priority)
            {
                _abilities[definition.Id] = definition;
            }
        }
        else
        {
            _abilities[definition.Id] = definition;
        }
    }

    public AbilityDefinition? Get(string id)
    {
        return _abilities.GetValueOrDefault(id);
    }

    public IEnumerable<AbilityDefinition> GetAll()
    {
        return _abilities.Values;
    }

    public IEnumerable<AbilityDefinition> GetByType(AbilityType type)
    {
        return _abilities.Values.Where(a => a.Type == type);
    }

    public IEnumerable<AbilityDefinition> GetByCategory(AbilityCategory category)
    {
        return _abilities.Values.Where(a => a.Category == category);
    }

    public bool Has(string id)
    {
        return _abilities.ContainsKey(id);
    }
}
