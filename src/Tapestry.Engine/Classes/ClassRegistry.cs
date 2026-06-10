namespace Tapestry.Engine.Classes;

public class ClassRegistry
{
    private readonly Dictionary<string, ClassDefinition> _classes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Plain write (upsert). Collision resolution is the RegistrationPolicy's job — by the
    /// time a write lands here, the policy has already elected the winner. Priority stays
    /// on the definition for parse compatibility but no longer arbitrates.
    /// </summary>
    public void Register(ClassDefinition definition)
    {
        _classes[definition.Id] = definition;
    }

    public ClassDefinition? Get(string id)
    {
        return _classes.GetValueOrDefault(id);
    }

    public IEnumerable<ClassDefinition> GetAll()
    {
        return _classes.Values;
    }

    public bool Has(string id)
    {
        return _classes.ContainsKey(id);
    }

    public IEnumerable<ClassDefinition> GetEligibleClasses(string raceCategory, string gender)
    {
        return _classes.Values.Where(c =>
            (c.AllowedCategories.Count == 0 || c.AllowedCategories.Contains(raceCategory, StringComparer.OrdinalIgnoreCase)) &&
            (c.AllowedGenders.Count == 0 || c.AllowedGenders.Contains(gender, StringComparer.OrdinalIgnoreCase)));
    }
}
