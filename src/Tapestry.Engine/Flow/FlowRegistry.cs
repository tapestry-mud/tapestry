namespace Tapestry.Engine.Flow;

public class FlowRegistry
{
    private readonly Dictionary<string, FlowDefinition> _flows =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Plain write (upsert). Collision resolution is the RegistrationPolicy's job — by the
    /// time a write lands here, the policy has already elected the winner.
    /// </summary>
    public void Register(FlowDefinition definition)
    {
        { _flows[definition.Id] = definition; }
    }

    public FlowDefinition? Get(string id)
    {
        { return _flows.GetValueOrDefault(id); }
    }

    public IReadOnlyList<FlowDefinition> GetByTrigger(string trigger)
    {
        {
            return _flows.Values
                .Where(f => string.Equals(f.Trigger, trigger, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public IReadOnlyList<FlowDefinition> All => _flows.Values.ToList();
}
