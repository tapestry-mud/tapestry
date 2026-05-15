namespace Tapestry.Engine.Quests;

public class QuestStateRepository
{
    private readonly Dictionary<Guid, QuestState> _states = new();

    public QuestState GetOrCreate(Guid playerId)
    {
        if (!_states.TryGetValue(playerId, out var state))
        {
            state = new QuestState();
            _states[playerId] = state;
        }
        return state;
    }

    public QuestState? Get(Guid playerId) =>
        _states.GetValueOrDefault(playerId);

    public void Set(Guid playerId, QuestState state)
    {
        _states[playerId] = state;
    }

    public IEnumerable<(Guid PlayerId, QuestState State)> All() =>
        _states.Select(kv => (kv.Key, kv.Value));
}
