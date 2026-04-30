// src/Tapestry.Engine/Mobs/DispositionEvaluator.cs
using Tapestry.Engine.Alignment;
using Tapestry.Shared;

namespace Tapestry.Engine.Mobs;

public class DispositionEvaluator
{
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly AlignmentManager _alignmentManager;
    // Per-tick dedup: cleared every mob tick to allow re-evaluation.
    private readonly Dictionary<(Guid mobId, Guid playerId), string> _cache = new();
    // Persistent state: tracks last dispatched reaction so we only fire on changes.
    // Cleared when a player leaves a room via player.moved subscription.
    private readonly Dictionary<(Guid mobId, Guid playerId), string> _reactionState = new();

    public DispositionEvaluator(World world, EventBus eventBus, AlignmentManager alignmentManager)
    {
        _world = world;
        _eventBus = eventBus;
        _alignmentManager = alignmentManager;

        // Clear reaction state for a player when they leave a room so they see
        // reactions fresh on next entry.
        _eventBus.Subscribe("player.moved", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            var playerId = evt.SourceEntityId.Value;
            var keys = _reactionState.Keys.Where(k => k.playerId == playerId).ToList();
            foreach (var k in keys) { _reactionState.Remove(k); }
        });
    }

    // Called at the start of each mob tick to reset the per-tick dedup cache.
    public void ClearCache()
    {
        _cache.Clear();
    }

    // Evaluate disposition of one mob toward one player.
    // aggroOnly=true: only hostile reactions dispatched, non-hostile left uncached so a
    // subsequent full call (from JS after room description) can pick them up.
    public void EvaluateForMob(Entity mob, Entity player, bool aggroOnly = false)
    {
        var key = (mob.Id, player.Id);
        if (_cache.ContainsKey(key)) { return; }

        var def = mob.GetProperty<DispositionDefinition>("disposition");
        if (def == null) { return; }

        var reaction = mob.HasTag("hostile") ? "hostile" : EvaluateRules(def, player);

        if (aggroOnly)
        {
            if (reaction == "hostile")
            {
                _cache[key] = reaction;
                _reactionState[key] = reaction;
                DispatchReaction(mob, player, reaction);
            }
            // Non-hostile: leave cache empty so the deferred JS call can evaluate them.
            return;
        }

        _cache[key] = reaction;

        // Only dispatch when reaction changes — prevents spam on recurring tick evaluations.
        // Hostile always dispatches (mob.aggro is idempotent via Engage's duplicate guard).
        if (reaction == "hostile" || !_reactionState.TryGetValue(key, out var last) || last != reaction)
        {
            _reactionState[key] = reaction;
            DispatchReaction(mob, player, reaction);
        }
    }

    // Evaluate all mobs in a room. aggroOnly=true is used by the immediate room-entry
    // path (inside MoveEntity) so hostile mobs engage before the player sees the room.
    // The deferred JS call passes aggroOnly=false to handle friendly/wary reactions.
    public void EvaluateRoom(string roomId, Guid playerId, bool aggroOnly = false)
    {
        var player = _world.GetEntity(playerId);
        if (player == null || !player.HasTag("player")) { return; }

        var room = _world.GetRoom(roomId);
        if (room == null) { return; }

        foreach (var entity in room.Entities.Where(e => e.HasTag("npc") && e.HasProperty("disposition")).ToList())
        {
            EvaluateForMob(entity, player, aggroOnly);
        }
    }

    private string EvaluateRules(DispositionDefinition def, Entity player)
    {
        var alignment = _alignmentManager.Get(player.Id);
        var bucket = _alignmentManager.Bucket(player.Id);
        foreach (var rule in def.Rules)
        {
            if (Matches(rule.When, alignment, bucket, player))
            {
                return rule.Reaction;
            }
        }
        return def.Default;
    }

    private static bool Matches(DispositionCondition cond, int alignment, string bucket, Entity player)
    {
        if (cond.MinAlignment.HasValue && alignment < cond.MinAlignment.Value)
        {
            return false;
        }
        if (cond.MaxAlignment.HasValue && alignment > cond.MaxAlignment.Value)
        {
            return false;
        }
        if (cond.Buckets != null && !cond.Buckets.Contains(bucket))
        {
            return false;
        }
        if (cond.HasTag != null && !player.HasTag(cond.HasTag))
        {
            return false;
        }
        return true;
    }

    private void DispatchReaction(Entity mob, Entity player, string reaction)
    {
        var data = new Dictionary<string, object?>
        {
            ["mobId"] = mob.Id.ToString(),
            ["playerId"] = player.Id.ToString()
        };

        switch (reaction)
        {
            case "hostile":
            {
                _eventBus.Publish(new GameEvent
                {
                    Type = "mob.aggro",
                    SourceEntityId = mob.Id,
                    TargetEntityId = player.Id,
                    RoomId = mob.LocationRoomId,
                    SourceEntityName = mob.Name,
                    Data = new Dictionary<string, object?>
                    {
                        ["attackerId"] = mob.Id.ToString(),
                        ["targetId"] = player.Id.ToString()
                    }
                });
                break;
            }
            case "wary":
            {
                _eventBus.Publish(new GameEvent
                {
                    Type = "mob.disposition.wary",
                    SourceEntityId = mob.Id,
                    TargetEntityId = player.Id,
                    RoomId = mob.LocationRoomId,
                    Data = data
                });
                break;
            }
            case "friendly":
            {
                _eventBus.Publish(new GameEvent
                {
                    Type = "mob.disposition.friendly",
                    SourceEntityId = mob.Id,
                    TargetEntityId = player.Id,
                    RoomId = mob.LocationRoomId,
                    Data = data
                });
                break;
            }
        }
    }
}
