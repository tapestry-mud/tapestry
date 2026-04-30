// src/Tapestry.Engine/Combat/CombatManager.cs
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Heartbeat;
using Tapestry.Shared;

namespace Tapestry.Engine.Combat;

public class CombatManager
{
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly List<ICombatPhase> _phases;
    private readonly Random _random;
    private readonly AbilityRegistry? _abilityRegistry;
    private readonly ProficiencyManager? _proficiencyManager;
    private readonly SessionManager? _sessionManager;
    private readonly EffectManager? _effectManager;
    private readonly AlignmentManager? _alignmentManager;
    private readonly Dictionary<Guid, List<Guid>> _combatLists = new();
    private readonly Dictionary<Guid, long> _fleeCooldowns = new();
    private const int FleeCooldownTicks = 80; // ~2 combat rounds (8 seconds)

    public CombatManager(World world, EventBus eventBus, List<ICombatPhase>? phases = null,
        Random? random = null, AbilityRegistry? abilityRegistry = null,
        ProficiencyManager? proficiencyManager = null, SessionManager? sessionManager = null,
        EffectManager? effectManager = null, AlignmentManager? alignmentManager = null)
    {
        _world = world;
        _eventBus = eventBus;
        _random = random ?? new Random();
        _abilityRegistry = abilityRegistry;
        _proficiencyManager = proficiencyManager;
        _sessionManager = sessionManager;
        _effectManager = effectManager;
        _alignmentManager = alignmentManager;

        if (phases != null)
        {
            _phases = phases.OrderBy(p => p.Priority).ToList();
        }
        else
        {
            _phases = new List<ICombatPhase>
            {
                new ResolveAutoAttacksPhase(),
                new ResolveStatusEffectsPhase(),
                new CheckWimpyPhase()
            }.OrderBy(p => p.Priority).ToList();
        }
    }

    public bool Engage(Entity attacker, Entity target, long currentTick = 0)
    {
        // Players are always engageable by mobs; "killable" guards quest NPCs and merchants.
        if (!target.HasTag("killable") && target.Type != "player")
        {
            return false;
        }

        if (target.HasTag("no-kill"))
        {
            return false;
        }

        if (attacker.LocationRoomId != null)
        {
            var room = _world.GetRoom(attacker.LocationRoomId);
            if (room != null && (room.HasTag("safe") || room.HasTag("no-combat")))
            {
                return false;
            }
        }

        if (HasFleeCooldown(attacker.Id, currentTick))
        {
            return false;
        }

        if (_combatLists.TryGetValue(attacker.Id, out var attackerList) && attackerList.Contains(target.Id))
        {
            return false;
        }

        AddToCombatList(attacker.Id, target.Id);
        AddToCombatList(target.Id, attacker.Id);

        _eventBus.Publish(new GameEvent
        {
            Type = "combat.engage",
            SourceEntityId = attacker.Id,
            TargetEntityId = target.Id,
            RoomId = attacker.LocationRoomId,
            SourceEntityName = attacker.Name,
            Data = new Dictionary<string, object?>
            {
                ["attackerName"] = attacker.Name,
                ["targetName"] = target.Name
            }
        });

        return true;
    }

    public bool IsInCombat(Guid entityId)
    {
        return _combatLists.TryGetValue(entityId, out var list) && list.Count > 0;
    }

    public Guid? GetPrimaryTarget(Guid entityId)
    {
        if (_combatLists.TryGetValue(entityId, out var list) && list.Count > 0)
        {
            return list[0];
        }
        return null;
    }

    /// <summary>
    /// Moves an opponent to the front of the combat list, making them the primary target.
    /// Used by taunt, rescue, threat mechanics, etc.
    /// </summary>
    public bool SetPrimaryTarget(Guid entityId, Guid newTargetId)
    {
        if (!_combatLists.TryGetValue(entityId, out var list) || !list.Contains(newTargetId))
        {
            return false;
        }
        list.Remove(newTargetId);
        list.Insert(0, newTargetId);
        return true;
    }

    public List<Guid> GetCombatList(Guid entityId)
    {
        if (_combatLists.TryGetValue(entityId, out var list))
        {
            return new List<Guid>(list);
        }
        return new List<Guid>();
    }

    public void RemoveFromCombat(Guid entityA, Guid entityB)
    {
        RemoveFromCombatList(entityA, entityB);
        RemoveFromCombatList(entityB, entityA);

        if (!IsInCombat(entityA))
        {
            PublishCombatEnd(entityA);
        }
        if (!IsInCombat(entityB))
        {
            PublishCombatEnd(entityB);
        }
    }

    public void RemoveEntityFromAllCombat(Guid entityId)
    {
        if (!_combatLists.TryGetValue(entityId, out var list))
        {
            return;
        }

        var opponents = new List<Guid>(list);
        foreach (var opponentId in opponents)
        {
            RemoveFromCombatList(opponentId, entityId);
            if (!IsInCombat(opponentId))
            {
                PublishCombatEnd(opponentId);
            }
        }

        _combatLists.Remove(entityId);
        PublishCombatEnd(entityId);
    }

    public List<Entity> GetCombatants()
    {
        var combatants = new List<Entity>();
        foreach (var entityId in _combatLists.Keys)
        {
            if (_combatLists[entityId].Count > 0)
            {
                var entity = _world.GetEntity(entityId);
                if (entity != null)
                {
                    combatants.Add(entity);
                }
            }
        }
        return combatants;
    }

    public bool AttemptFlee(Entity entity, PulseContext context)
    {
        if (entity.HasTag("no_flee"))
        {
            context.EventBus.Publish(new GameEvent
            {
                Type = "combat.flee.prevented",
                SourceEntityId = entity.Id,
                RoomId = entity.LocationRoomId,
                SourceEntityName = entity.Name,
                Data = new Dictionary<string, object?>
                {
                    ["entityName"] = entity.Name,
                    ["reason"] = "no_flee"
                }
            });
            return false;
        }

        if (entity.LocationRoomId == null)
        {
            return false;
        }

        var room = _world.GetRoom(entity.LocationRoomId);
        if (room == null)
        {
            return false;
        }

        var exits = room.AvailableExits().ToList();
        if (exits.Count == 0)
        {
            context.EventBus.Publish(new GameEvent
            {
                Type = "combat.flee.failed",
                SourceEntityId = entity.Id,
                RoomId = entity.LocationRoomId,
                SourceEntityName = entity.Name,
                Data = new Dictionary<string, object?>
                {
                    ["entityName"] = entity.Name
                }
            });
            return false;
        }

        var direction = exits[context.Random.Next(exits.Count)];
        var fromRoom = entity.LocationRoomId;

        RemoveEntityFromAllCombat(entity.Id);
        _world.MoveEntity(entity, direction);
        _fleeCooldowns[entity.Id] = context.CurrentTick + FleeCooldownTicks;

        context.EventBus.Publish(new GameEvent
        {
            Type = "combat.flee",
            SourceEntityId = entity.Id,
            RoomId = fromRoom,
            SourceEntityName = entity.Name,
            Data = new Dictionary<string, object?>
            {
                ["entityName"] = entity.Name,
                ["fromRoom"] = fromRoom,
                ["toRoom"] = entity.LocationRoomId,
                ["direction"] = direction.ToString().ToLower()
            }
        });

        return true;
    }

    public bool HasFleeCooldown(Guid entityId, long currentTick)
    {
        if (_fleeCooldowns.TryGetValue(entityId, out var expiresAt))
        {
            return currentTick < expiresAt;
        }
        return false;
    }

    public void HandleEntityDeath(Guid entityId, Guid? killerId)
    {
        if (killerId.HasValue)
        {
            if (_alignmentManager != null)
            {
                _alignmentManager.Shift(killerId.Value, 0, "combat.kill",
                    new Dictionary<string, object?> { ["targetId"] = entityId.ToString() });
            }

            var killer = _world.GetEntity(killerId.Value);
            var victim = _world.GetEntity(entityId);

            _eventBus.Publish(new GameEvent
            {
                Type = "combat.kill",
                SourceEntityId = killerId,
                TargetEntityId = entityId,
                RoomId = victim?.LocationRoomId ?? killer?.LocationRoomId,
                SourceEntityName = killer?.Name,
                Data = new Dictionary<string, object?>
                {
                    ["killerName"] = killer?.Name,
                    ["victimName"] = victim?.Name
                }
            });
        }

        RemoveEntityFromAllCombat(entityId);
    }

    private void AddToCombatList(Guid entityId, Guid opponentId)
    {
        if (!_combatLists.TryGetValue(entityId, out var list))
        {
            list = new List<Guid>();
            _combatLists[entityId] = list;
        }

        if (!list.Contains(opponentId))
        {
            list.Add(opponentId);
        }
    }

    private void RemoveFromCombatList(Guid entityId, Guid opponentId)
    {
        if (_combatLists.TryGetValue(entityId, out var list))
        {
            list.Remove(opponentId);
            if (list.Count == 0)
            {
                _combatLists.Remove(entityId);
            }
        }
    }

    private void PublishCombatEnd(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        _eventBus.Publish(new GameEvent
        {
            Type = "combat.end",
            SourceEntityId = entityId,
            RoomId = entity?.LocationRoomId,
            SourceEntityName = entity?.Name
        });
    }
}
