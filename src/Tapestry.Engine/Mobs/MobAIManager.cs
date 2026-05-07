// src/Tapestry.Engine/Mobs/MobAIManager.cs
using Microsoft.Extensions.Logging;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Heartbeat;
using Tapestry.Shared;

namespace Tapestry.Engine.Mobs;

public class MobContext
{
    public Guid EntityId { get; init; }
    public string Name { get; init; } = "";
    public string RoomId { get; init; } = "";
    public string Behavior { get; init; } = "";
}

public class MobAIManager
{
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly CombatManager _combat;
    private readonly DispositionEvaluator _dispositionEvaluator;
    private readonly ILogger<MobAIManager> _logger;
    private readonly HashSet<string> _activeAreas = new();
    private readonly Dictionary<string, int> _areaPlayerCounts = new();
    private readonly Dictionary<string, Action<MobContext>> _behaviors = new();
    private readonly Dictionary<Guid, long> _lastActionTick = new();
    private long _currentTick;

    public MobAIManager(World world, EventBus eventBus, CombatManager combat,
        DispositionEvaluator dispositionEvaluator, ILogger<MobAIManager> logger)
    {
        _world = world;
        _eventBus = eventBus;
        _combat = combat;
        _dispositionEvaluator = dispositionEvaluator;
        _logger = logger;
    }

    public void RegisterBehavior(string name, Action<MobContext> handler)
    {
        _behaviors[name] = handler;
    }

    public bool HasBehavior(string name)
    {
        return _behaviors.ContainsKey(name);
    }

    public void ActivateArea(string area)
    {
        _activeAreas.Add(area);
    }

    public void DeactivateArea(string area)
    {
        _activeAreas.Remove(area);
    }

    public bool IsAreaActive(string area)
    {
        return _activeAreas.Contains(area);
    }

    public static string GetAreaFromRoomId(string roomId)
    {
        var colonIndex = roomId.IndexOf(':');
        return colonIndex >= 0 ? roomId[..colonIndex] : roomId;
    }

    public void PlayerEnteredRoom(string roomId)
    {
        var area = GetAreaFromRoomId(roomId);
        if (!_areaPlayerCounts.ContainsKey(area))
        {
            _areaPlayerCounts[area] = 0;
        }
        _areaPlayerCounts[area]++;
        _activeAreas.Add(area);
    }

    public void PlayerLeftRoom(string roomId)
    {
        var area = GetAreaFromRoomId(roomId);
        if (_areaPlayerCounts.ContainsKey(area))
        {
            _areaPlayerCounts[area]--;
            if (_areaPlayerCounts[area] <= 0)
            {
                _areaPlayerCounts.Remove(area);
                _activeAreas.Remove(area);
            }
        }
    }

    public void Tick()
    {
        _currentTick++;
        _dispositionEvaluator.ClearCache();

        foreach (var entity in _world.GetEntitiesByTag("npc"))
        {
            if (entity.LocationRoomId == null)
            {
                continue;
            }

            var area = GetAreaFromRoomId(entity.LocationRoomId);
            if (!_activeAreas.Contains(area))
            {
                continue;
            }

            var behavior = entity.GetProperty<string>(MobProperties.Behavior);

            if (behavior != null && !TryFlee(entity))
            {
                if (_behaviors.TryGetValue(behavior, out var handler))
                {
                    var context = new MobContext
                    {
                        EntityId = entity.Id,
                        Name = entity.Name,
                        RoomId = entity.LocationRoomId,
                        Behavior = behavior
                    };
                    try
                    {
                        handler(context);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Mob AI error: entity={EntityId} name={Name} behavior={Behavior}",
                            entity.Id, entity.Name, behavior);
                    }
                }

                // Publish even if no handler is registered: mob.ai.tick signals the mob was
                // considered this tick, regardless of whether a behavior handler ran.
                _eventBus.Publish(new GameEvent
                {
                    Type = "mob.ai.tick",
                    SourceEntityId = entity.Id,
                    RoomId = entity.LocationRoomId,
                    Data = new Dictionary<string, object?>
                    {
                        ["entityId"] = entity.Id.ToString(),
                        ["name"] = entity.Name,
                        ["roomId"] = entity.LocationRoomId,
                        ["behavior"] = behavior
                    }
                });
            }

            if (entity.HasProperty("disposition"))
            {
                var room = _world.GetRoom(entity.LocationRoomId);
                if (room != null)
                {
                    foreach (var player in room.Entities.Where(e => e.HasTag("player")).ToList())
                    {
                        _dispositionEvaluator.EvaluateForMob(entity, player);
                    }
                }
            }
        }
    }

    // Called immediately on room entry (inside MoveEntity) — hostile only.
    // Guarantees aggro mobs engage before the player sees the room description.
    public void OnPlayerEnteredRoom(string roomId, Guid playerId)
    {
        _dispositionEvaluator.EvaluateRoom(roomId, playerId, aggroOnly: true);
    }

    // Called from JS after room description is sent — friendly/wary reactions.
    // Non-hostile reactions are deferred so messages appear after the room desc.
    public void TriggerDisposition(string roomId, Guid playerId)
    {
        _dispositionEvaluator.EvaluateRoom(roomId, playerId, aggroOnly: false);
    }

    // Called when a mob moves into a room — evaluates against all players present.
    // No ordering concern here (no room description), so all reactions fire immediately.
    public void OnMobEnteredRoom(Entity mob, string roomId)
    {
        var room = _world.GetRoom(roomId);
        if (room == null) { return; }
        foreach (var player in room.Entities.Where(e => e.HasTag("player")).ToList())
        {
            _dispositionEvaluator.EvaluateForMob(mob, player, aggroOnly: false);
        }
    }

    private bool TryFlee(Entity entity)
    {
        var raw = entity.GetProperty<object>("flee_threshold");
        if (raw == null)
        {
            return false;
        }

        if (!double.TryParse(raw.ToString(), out var threshold) || threshold <= 0)
        {
            return false;
        }

        if (!_combat.IsInCombat(entity.Id))
        {
            return false;
        }

        var hpPercent = entity.Stats.MaxHp > 0
            ? (double)entity.Stats.Hp / entity.Stats.MaxHp
            : 0;

        if (hpPercent >= threshold)
        {
            return false;
        }

        var pulseContext = new PulseContext
        {
            CurrentTick = _currentTick,
            World = _world,
            EventBus = _eventBus,
            CombatManager = _combat,
            Random = Random.Shared
        };

        return _combat.AttemptFlee(entity, pulseContext);
    }

    public void RecordAction(Guid entityId)
    {
        _lastActionTick[entityId] = _currentTick;
    }

    public long GetTicksSinceLastAction(Guid entityId)
    {
        if (_lastActionTick.TryGetValue(entityId, out var lastTick))
        {
            return _currentTick - lastTick;
        }
        return long.MaxValue;
    }

    public void IncrementTick()
    {
        _currentTick++;
    }
}
