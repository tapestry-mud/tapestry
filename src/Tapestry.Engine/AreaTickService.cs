using Tapestry.Data;
using Tapestry.Shared;

namespace Tapestry.Engine;

public class AreaTickService
{
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly AreaRegistry _areaRegistry;
    private readonly ServerConfig _config;
    private readonly Dictionary<string, AreaTickState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    public AreaTickService(World world, EventBus eventBus, AreaRegistry areaRegistry, ServerConfig config)
    {
        _world = world;
        _eventBus = eventBus;
        _areaRegistry = areaRegistry;
        _config = config;
    }

    public void Tick()
    {
        foreach (var areaDef in _areaRegistry.All())
        {
            var state = GetOrCreateState(areaDef.Id);
            var playerCount = GetPlayerCount(areaDef.Id);

            state.TicksSinceLastFire++;

            var baseInterval = state.OverrideResetInterval ?? areaDef.ResetInterval;
            var modifier = playerCount > 0
                ? (state.OverrideOccupiedModifier ?? areaDef.OccupiedModifier)
                : 1.0f;
            var effectiveInterval = (long)(baseInterval * modifier);

            if (state.TicksSinceLastFire < effectiveInterval) { continue; }

            state.TicksSinceLastFire = 0;
            state.TickCount++;

            _eventBus.Publish(new GameEvent
            {
                Type = "area.tick",
                Data = new Dictionary<string, object?>
                {
                    ["areaId"] = areaDef.Id,
                    ["tickCount"] = state.TickCount,
                    ["playerCount"] = playerCount
                }
            });
        }
    }

    public AreaTickState? GetAreaState(string areaId)
    {
        return _states.GetValueOrDefault(areaId);
    }

    public int GetPlayerCount(string areaId)
    {
        return _world.AllRooms
            .Where(r => string.Equals(r.Area, areaId, StringComparison.OrdinalIgnoreCase))
            .Sum(r => r.Entities.Count(e => e.Type == "player"));
    }

    public void SetResetInterval(string areaId, int ticks)
    {
        GetOrCreateState(areaId).OverrideResetInterval = ticks;
    }

    public void SetOccupiedModifier(string areaId, float modifier)
    {
        GetOrCreateState(areaId).OverrideOccupiedModifier = modifier;
    }

    private AreaTickState GetOrCreateState(string areaId)
    {
        if (_states.TryGetValue(areaId, out var existing)) { return existing; }
        var state = new AreaTickState { AreaId = areaId };
        _states[areaId] = state;
        return state;
    }
}
