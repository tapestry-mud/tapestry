namespace Tapestry.Engine.Races;

public class RaceRegistry
{
    private readonly Dictionary<string, RaceDefinition> _races = new(StringComparer.OrdinalIgnoreCase);

    public void Register(RaceDefinition definition)
    {
        if (_races.TryGetValue(definition.Id, out var existing))
        {
            if (definition.Priority > existing.Priority)
            {
                _races[definition.Id] = definition;
            }
        }
        else
        {
            _races[definition.Id] = definition;
        }
    }

    public RaceDefinition? Get(string id)
    {
        return _races.GetValueOrDefault(id);
    }

    public IEnumerable<RaceDefinition> GetAll()
    {
        return _races.Values;
    }

    public bool Has(string id)
    {
        return _races.ContainsKey(id);
    }
}
