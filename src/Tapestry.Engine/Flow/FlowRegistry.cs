namespace Tapestry.Engine.Flow;

public class FlowRegistry
{
    private readonly Dictionary<string, FlowDefinition> _flows =
        new(StringComparer.OrdinalIgnoreCase);

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
