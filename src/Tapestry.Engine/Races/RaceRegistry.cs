using Tapestry.Engine.Registration;

namespace Tapestry.Engine.Races;

public class RaceRegistry
{
    private readonly Dictionary<string, RaceDefinition> _races = new(StringComparer.OrdinalIgnoreCase);
    private readonly RegistrationGate? _gate;

    public RaceRegistry(RegistrationGate? gate = null)
    {
        _gate = gate;
    }

    /// <summary>
    /// Plain write (upsert). Collision resolution is the RegistrationPolicy's job — by the
    /// time a write lands here, the policy has already elected the winner. Priority stays
    /// on the definition for parse compatibility but no longer arbitrates.
    /// </summary>
    public void Register(RaceDefinition definition)
    {
        _gate?.AssertCommitScope("race", definition.Id);
        _races[definition.Id] = definition;
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
