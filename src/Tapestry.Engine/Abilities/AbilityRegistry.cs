namespace Tapestry.Engine.Abilities;

public class AbilityRegistry
{
    private readonly Dictionary<string, AbilityDefinition> _abilities = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Plain write (upsert). Collision resolution is the RegistrationPolicy's job — by the
    /// time a write lands here, the policy has already elected the winner. Priority stays
    /// on the definition for parse compatibility but no longer arbitrates.
    /// </summary>
    public void Register(AbilityDefinition definition)
    {
        _abilities[definition.Id] = definition;
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
