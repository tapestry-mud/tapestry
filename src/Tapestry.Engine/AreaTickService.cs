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
    private readonly Dictionary<string, int> _playerCounts =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _areaTickData = new(3);

    public AreaTickService(World world, EventBus eventBus, AreaRegistry areaRegistry, ServerConfig config)
    {
        _world = world;
        _eventBus = eventBus;
        _areaRegistry = areaRegistry;
        _config = config;
    }

    public void Tick()
    {
        RebuildPlayerCounts();

        foreach (var areaDef in _areaRegistry.All())
        {
            var state = GetOrCreateState(areaDef.Id);
            _playerCounts.TryGetValue(areaDef.Id, out var playerCount);

            state.TicksSinceLastFire++;

            var baseInterval = state.OverrideResetInterval ?? areaDef.ResetInterval;
            // Repop-off: a non-positive effective interval disables the recurring reset
            // entirely (solo/oracle areas populate once at boot, then never repop). Boot
            // population is decoupled - GameLoopService calls RunAreaReset directly - so
            // this gate only suppresses the tick-driven reset, never the initial fill.
            if (baseInterval <= 0) { continue; }
            var modifier = playerCount > 0
                ? (state.OverrideOccupiedModifier ?? areaDef.OccupiedModifier)
                : 1.0f;
            var effectiveInterval = (long)(baseInterval * modifier);

            if (state.TicksSinceLastFire < effectiveInterval) { continue; }

            state.TicksSinceLastFire = 0;
            state.TickCount++;

            _areaTickData.Clear();
            _areaTickData["areaId"] = areaDef.Id;
            _areaTickData["tickCount"] = state.TickCount;
            _areaTickData["playerCount"] = playerCount;
            _eventBus.Publish(new GameEvent
            {
                Type = "area.tick",
                Data = _areaTickData
            });
        }
    }

    public AreaTickState? GetAreaState(string areaId)
    {
        return _states.GetValueOrDefault(areaId);
    }

    public int GetPlayerCount(string areaId)
    {
        if (_playerCounts.Count == 0)
        {
            RebuildPlayerCounts();
        }
        _playerCounts.TryGetValue(areaId, out var count);
        return count;
    }

    private void RebuildPlayerCounts()
    {
        _playerCounts.Clear();
        foreach (var room in _world.AllRooms)
        {
            if (room.Area == null) { continue; }
            var players = 0;
            for (var i = 0; i < room.Entities.Count; i++)
            {
                if (room.Entities[i].Type == EntityTypes.Player)
                {
                    players++;
                }
            }
            if (players > 0)
            {
                _playerCounts.TryGetValue(room.Area, out var existing);
                _playerCounts[room.Area] = existing + players;
            }
        }
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
