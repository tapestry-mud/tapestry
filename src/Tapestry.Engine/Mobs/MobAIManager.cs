// src/Tapestry.Engine/Mobs/MobAIManager.cs
using Microsoft.Extensions.Logging;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Heartbeat;
using Tapestry.Engine.Registration;
using Tapestry.Engine.Rest;
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
    private readonly TapestryMetrics _metrics;
    private readonly HashSet<string> _activeAreas = new();
    private readonly Dictionary<string, int> _areaPlayerCounts = new();
    private readonly Dictionary<string, Action<MobContext>> _behaviors = new();
    private readonly Dictionary<string, string> _behaviorOwners = new();
    private readonly Dictionary<string, int> _strikes = new();
    private readonly HashSet<string> _quarantined = new();
    private readonly Dictionary<Guid, long> _lastActionTick = new();
    private long _currentTick;

    private readonly RegistrationGate? _gate;
    private readonly TimeProvider _time;
    private readonly MobAiSection _budget;

    private Guid? _cursor;
    private int _cursorLag;

    /// <summary>Mobs deferred to the next tick by the latest sweep's budget (0 = full sweep).</summary>
    public int CurrentCursorLag => _cursorLag;

    public MobAIManager(World world, EventBus eventBus, CombatManager combat,
        DispositionEvaluator dispositionEvaluator, ILogger<MobAIManager> logger,
        TapestryMetrics metrics, RegistrationGate? gate = null,
        ServerConfig? config = null, TimeProvider? time = null)
    {
        _world = world;
        _eventBus = eventBus;
        _combat = combat;
        _dispositionEvaluator = dispositionEvaluator;
        _logger = logger;
        _metrics = metrics;
        _gate = gate;
        _budget = config?.MobAi ?? new MobAiSection();
        _time = time ?? TimeProvider.System;
        metrics.RegisterMobAiCursorLag(() => _cursorLag);
    }

    public void RegisterBehavior(string name, Action<MobContext> handler, string owner = "kernel")
    {
        _gate?.AssertCommitScope("mob-behavior", name);
        _behaviors[name] = handler;
        _behaviorOwners[name] = owner;
    }

    public bool HasBehavior(string name)
    {
        return _behaviors.ContainsKey(name);
    }

    public bool IsBehaviorQuarantined(string name)
    {
        return _quarantined.Contains(name);
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

        long behaviorTicks = 0;
        long publishTicks = 0;
        long dispositionTicks = 0;

        // Occupancy-driven sweep (WeatherService precedent): enumerate only rooms in
        // player-active areas and take their npc contents, instead of scanning every
        // entity in the world. Ordered by entity id: the stable ordering the
        // round-robin cursor resumes against.
        var tickStart = _time.GetTimestamp();
        var scanStart = _time.GetTimestamp();
        var npcs = _world.AllRooms
            .Where(r => _activeAreas.Contains(GetAreaFromRoomId(r.Id)))
            .SelectMany(r => r.Entities.Where(e => e.Type == EntityTypes.Npc))
            .OrderBy(e => e.Id)
            .ToList();
        var scanTicks = _time.GetTimestamp() - scanStart;

        // Round-robin from the cursor: budget checked BETWEEN mobs (a single mob's
        // overshoot is bounded by the invocation cap), minimum one mob per tick so
        // the sweep always makes progress. Deferred mobs resume here next tick --
        // uniform staleness across the world.
        var budgetTicks = _budget.TickBudgetMs * _time.TimestampFrequency / 1000;

        var start = 0;
        if (_cursor is { } cursor)
        {
            start = npcs.FindIndex(e => e.Id.CompareTo(cursor) > 0);
            if (start < 0)
            {
                start = 0;
            }
        }

        var processed = 0;
        for (var i = 0; i < npcs.Count; i++)
        {
            if (processed > 0 && _time.GetTimestamp() - tickStart >= budgetTicks)
            {
                _metrics.MobAiBudgetExhausted.Add(1);
                break;
            }
            var entity = npcs[(start + i) % npcs.Count];
            ProcessMob(entity, ref behaviorTicks, ref publishTicks, ref dispositionTicks);
            processed++;
            _cursor = entity.Id;
        }

        _cursorLag = npcs.Count - processed;
        if (processed == npcs.Count)
        {
            _cursor = null;
        }

        RecordPhase("scan", scanTicks);
        RecordPhase("behavior", behaviorTicks);
        RecordPhase("publish", publishTicks);
        RecordPhase("disposition", dispositionTicks);
    }

    private void ProcessMob(Entity entity, ref long behaviorTicks, ref long publishTicks,
        ref long dispositionTicks)
    {
        if (entity.LocationRoomId == null)
        {
            return;
        }

        // Posture gate: a resting or sleeping mob doesn't run its idle/wander behavior
        // (both require an awake, standing mob -- ROM mobiles only act when standing).
        // mob.ai.tick drives idle AND battle commands; both are moot here -- combat
        // auto-wake flips a mob back to awake before any battle behavior matters.
        // Disposition still runs below, so an aggressive mob can notice an intruder and
        // wake via the resulting engage. Lives on the budget/cursor path: a settled mob
        // costs one property read and is otherwise skipped.
        var restState = entity.GetProperty<string?>(RestProperties.RestState) ?? RestProperties.StateAwake;
        var settled = restState == RestProperties.StateResting || restState == RestProperties.StateSleeping;

        var behavior = entity.GetProperty<string>(MobProperties.Behavior);

        if (!settled && behavior != null && !TryFlee(entity))
        {
            if (!_quarantined.Contains(behavior)
                && _behaviors.TryGetValue(behavior, out var handler))
            {
                var context = new MobContext
                {
                    EntityId = entity.Id,
                    Name = entity.Name,
                    RoomId = entity.LocationRoomId,
                    Behavior = behavior
                };
                var behaviorStart = _time.GetTimestamp();
                var interrupted = false;
                try
                {
                    handler(context);
                }
                catch (MobBudgetExceededException ex)
                {
                    interrupted = true;
                    _logger.LogWarning(ex,
                        "Mob behavior interrupted at its invocation cap: entity={EntityId} name={Name} behavior={Behavior}",
                        entity.Id, entity.Name, behavior);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Mob AI error: entity={EntityId} name={Name} behavior={Behavior}",
                        entity.Id, entity.Name, behavior);
                }
                var elapsed = _time.GetTimestamp() - behaviorStart;
                behaviorTicks += elapsed;

                var capTicks = _budget.InvocationCapMs * _time.TimestampFrequency / 1000;
                if (interrupted || elapsed >= capTicks)
                {
                    RecordStrike(behavior, entity);
                }
            }

            // Publish even if no handler is registered: mob.ai.tick signals the mob was
            // considered this tick, regardless of whether a behavior handler ran.
            // Quarantined behaviors must NOT skip this block -- the mob is still alive.
            var publishStart = _time.GetTimestamp();
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
            publishTicks += _time.GetTimestamp() - publishStart;
        }

        if (entity.DispositionRules != null || entity.Disposition == Disposition.Hostile)
        {
            var dispositionStart = _time.GetTimestamp();
            var room = _world.GetRoom(entity.LocationRoomId);
            if (room != null)
            {
                foreach (var player in room.Entities.Where(e => e.Type == EntityTypes.Player).ToList())
                {
                    _dispositionEvaluator.EvaluateForMob(entity, player);
                }
            }
            dispositionTicks += _time.GetTimestamp() - dispositionStart;
        }
    }

    private void RecordStrike(string behavior, Entity entity)
    {
        _metrics.MobAiInvocationCap.Add(1,
            new KeyValuePair<string, object?>("behavior", behavior));

        var strikes = _strikes.GetValueOrDefault(behavior) + 1;
        _strikes[behavior] = strikes;
        var owner = _behaviorOwners.GetValueOrDefault(behavior, "kernel");

        _logger.LogWarning(
            "Mob behavior '{Behavior}' (pack {Pack}) blew its {CapMs}ms invocation cap: strike {Strikes}/{Max} (entity={EntityId} name={Name})",
            behavior, owner, _budget.InvocationCapMs, strikes, _budget.QuarantineStrikes,
            entity.Id, entity.Name);

        if (strikes >= _budget.QuarantineStrikes && _quarantined.Add(behavior))
        {
            _metrics.MobAiQuarantine.Add(1,
                new KeyValuePair<string, object?>("behavior", behavior),
                new KeyValuePair<string, object?>("pack", owner));
            _logger.LogError(
                "Mob behavior '{Behavior}' from pack '{Pack}' QUARANTINED after {Strikes} budget strikes. Its mobs are inert (publish/disposition unaffected) until reboot. Fix the script and redeploy.",
                behavior, owner, strikes);
        }
    }

    private void RecordPhase(string phase, long elapsedTicks)
    {
        var ms = elapsedTicks * 1000.0 / _time.TimestampFrequency;
        _metrics.MobAiPhaseMs.Record(ms, new KeyValuePair<string, object?>("phase", phase));
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
        foreach (var player in room.Entities.Where(e => e.Type == EntityTypes.Player).ToList())
        {
            _dispositionEvaluator.EvaluateForMob(mob, player, aggroOnly: false);
        }
    }

    private bool TryFlee(Entity entity)
    {
        if (!_combat.IsInCombat(entity.Id))
        {
            return false;
        }

        if (!_combat.ShouldFlee(entity, _currentTick))
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
