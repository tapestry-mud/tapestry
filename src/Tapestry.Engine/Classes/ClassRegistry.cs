namespace Tapestry.Engine.Classes;

public class ClassRegistry
{
    private readonly Dictionary<string, ClassDefinition> _classes = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ClassDefinition definition)
    {
        if (_classes.TryGetValue(definition.Id, out var existing))
        {
            if (definition.Priority > existing.Priority)
            {
                _classes[definition.Id] = definition;
            }
        }
        else
        {
            _classes[definition.Id] = definition;
        }
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
