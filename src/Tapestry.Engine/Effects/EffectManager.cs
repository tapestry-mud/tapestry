using Tapestry.Shared;

namespace Tapestry.Engine.Effects;

public class EffectManager
{
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly Dictionary<Guid, List<ActiveEffect>> _activeEffects = new();

    public EffectManager(World world, EventBus eventBus)
    {
        _world = world;
        _eventBus = eventBus;
    }

    public bool TryApply(ActiveEffect effect)
    {
        var entity = _world.GetEntity(effect.TargetEntityId);
        if (entity == null)
        {
            return false;
        }

        if (!_activeEffects.TryGetValue(effect.TargetEntityId, out var effects))
        {
            effects = new List<ActiveEffect>();
            _activeEffects[effect.TargetEntityId] = effects;
        }

        // Effect ID gate — reject if already present
        if (effects.Any(e => e.Id == effect.Id))
        {
            return false;
        }

        effects.Add(effect);
        ApplyEffectToEntity(entity, effect);

        _eventBus.Publish(new GameEvent
        {
            Type = "effect.applied",
            SourceEntityId = effect.SourceEntityId,
            TargetEntityId = effect.TargetEntityId,
            RoomId = entity.LocationRoomId,
            Data = new Dictionary<string, object?>
            {
                ["effectId"] = effect.Id,
                ["sourceAbilityId"] = effect.SourceAbilityId,
                ["duration"] = effect.RemainingPulses
            }
        });
        return true;
    }

    public void Remove(Guid entityId, string effectId)
    {
        if (!_activeEffects.TryGetValue(entityId, out var effects))
        {
            return;
        }

        var effect = effects.FirstOrDefault(e => e.Id == effectId);
        if (effect == null)
        {
            return;
        }

        var entity = _world.GetEntity(entityId);
        if (entity != null)
        {
            RemoveEffectFromEntity(entity, effect);
        }

        effects.Remove(effect);

        _eventBus.Publish(new GameEvent
        {
            Type = "effect.removed",
            TargetEntityId = entityId,
            Data = new Dictionary<string, object?> { ["effectId"] = effectId }
        });
    }

    public void RemoveByFlag(Guid entityId, string flag)
    {
        if (!_activeEffects.TryGetValue(entityId, out var effects))
        {
            return;
        }

        var matching = effects.Where(e => e.Flags.Contains(flag)).ToList();
        foreach (var effect in matching)
        {
            var entity = _world.GetEntity(entityId);
            if (entity != null)
            {
                RemoveEffectFromEntity(entity, effect);
            }

            effects.Remove(effect);

            _eventBus.Publish(new GameEvent
            {
                Type = "effect.removed",
                TargetEntityId = entityId,
                Data = new Dictionary<string, object?> { ["effectId"] = effect.Id }
            });
        }
    }

    public bool HasEffect(Guid entityId, string effectId)
    {
        if (!_activeEffects.TryGetValue(entityId, out var effects))
        {
            return false;
        }
        return effects.Any(e => e.Id == effectId);
    }

    public List<ActiveEffect> GetActive(Guid entityId)
    {
        if (!_activeEffects.TryGetValue(entityId, out var effects))
        {
            return new List<ActiveEffect>();
        }
        return effects.ToList();
    }

    public void TickPulse()
    {
        var expiredList = new List<(Guid EntityId, ActiveEffect Effect)>();

        foreach (var (entityId, effects) in _activeEffects)
        {
            foreach (var effect in effects)
            {
                if (effect.RemainingPulses < 0)
                {
                    continue; // permanent effect
                }

                effect.RemainingPulses--;
                if (effect.RemainingPulses <= 0)
                {
                    expiredList.Add((entityId, effect));
                }
            }
        }

        foreach (var (entityId, effect) in expiredList)
        {
            var entity = _world.GetEntity(entityId);
            if (entity != null)
            {
                RemoveEffectFromEntity(entity, effect);
            }

            _activeEffects[entityId].Remove(effect);

            _eventBus.Publish(new GameEvent
            {
                Type = "effect.expired",
                TargetEntityId = entityId,
                RoomId = entity?.LocationRoomId,
                Data = new Dictionary<string, object?>
                {
                    ["effectId"] = effect.Id,
                    ["sourceAbilityId"] = effect.SourceAbilityId
                }
            });
        }
    }

    private void ApplyEffectToEntity(Entity entity, ActiveEffect effect)
    {
        foreach (var mod in effect.StatModifiers)
        {
            // Guard against duplicate modifiers when effects are re-applied after a save/load
            // (EffectManager state is not persisted, but stat modifiers are)
            var alreadyApplied = entity.Stats.Modifiers.Any(
                m => m.Source == mod.Source && m.Stat == mod.Stat);
            if (!alreadyApplied)
            {
                entity.Stats.AddModifier(mod);
            }
        }
        foreach (var flag in effect.Flags)
        {
            entity.AddTag(flag);
        }
    }

    private void RemoveEffectFromEntity(Entity entity, ActiveEffect effect)
    {
        foreach (var mod in effect.StatModifiers)
        {
            entity.Stats.RemoveModifiersBySource(mod.Source);
        }
        foreach (var flag in effect.Flags)
        {
            entity.RemoveTag(flag);
        }
    }
}
