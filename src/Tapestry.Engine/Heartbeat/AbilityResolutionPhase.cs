using Tapestry.Engine.Abilities;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Races;
using Tapestry.Engine.Rest;
using Tapestry.Engine.Stats;
using Tapestry.Shared;

namespace Tapestry.Engine.Heartbeat;

public class AbilityResolutionPhase : IPulseHandler
{
    public string Name => "AbilityResolution";
    public int Cadence { get; init; } = 1;
    public int Priority { get; init; } = 100;

    // Ephemeral pulse-delay tracking: entityId -> (abilityId -> nextReadyPulse)
    private readonly Dictionary<Guid, Dictionary<string, long>> _pulseDelays = new();

    private readonly RaceRegistry? _raceRegistry;
    private readonly AlignmentManager? _alignmentManager;

    public AbilityResolutionPhase() { }

    public AbilityResolutionPhase(RaceRegistry? raceRegistry = null, AlignmentManager? alignmentManager = null)
    {
        _raceRegistry = raceRegistry;
        _alignmentManager = alignmentManager;
    }

    private RaceDefinition? GetRaceFor(Entity entity)
    {
        if (_raceRegistry == null) { return null; }
        var raceId = entity.GetProperty<string>("race");
        return string.IsNullOrEmpty(raceId) ? null : _raceRegistry.Get(raceId);
    }

    public void Execute(PulseContext context)
    {
        var entities = context.World.GetAllTrackedEntities()
            .Where(e => e.HasProperty(AbilityProperties.QueuedActions))
            .ToList();

        foreach (var entity in entities)
        {
            ProcessEntityQueue(entity, context);
        }
    }

    public void ClearPulseDelays(Guid entityId)
    {
        _pulseDelays.Remove(entityId);
    }

    private void ProcessEntityQueue(Entity entity, PulseContext context)
    {
        var queue = entity.GetProperty<List<object>>(AbilityProperties.QueuedActions);
        if (queue == null || queue.Count == 0)
        {
            entity.SetProperty(AbilityProperties.QueuedActions, null);
            return;
        }

        while (queue.Count > 0)
        {
            var front = queue[0] as Dictionary<string, object?>;
            if (front == null)
            {
                queue.RemoveAt(0);
                continue;
            }

            var abilityId = front.GetValueOrDefault("abilityId") as string;
            if (abilityId == null)
            {
                queue.RemoveAt(0);
                continue;
            }

            var definition = context.AbilityRegistry.Get(abilityId);
            if (definition == null)
            {
                PublishFizzle(entity, abilityId, "unknown_ability", context);
                queue.RemoveAt(0);
                continue;
            }

            var validation = Validate(entity, definition, front, context);
            if (validation != ValidationResult.Ok)
            {
                PublishFizzle(entity, abilityId, validation.ToString().ToLower(), context);
                queue.RemoveAt(0);
                continue;
            }

            // Valid ability found — execute it
            Resolve(entity, definition, front, context);
            queue.RemoveAt(0);

            // One valid execution per pulse — stop
            break;
        }

        if (queue.Count == 0)
        {
            entity.SetProperty(AbilityProperties.QueuedActions, null);
        }
    }

    private enum ValidationResult
    {
        Ok,
        Asleep,
        Alignment_Restricted,
        No_Proficiency,
        Initiate_Only,
        Invalid_Target,
        Not_In_Combat,
        Effect_Present,
        Pulse_Delay,
        Insufficient_Resources
    }

    private ValidationResult Validate(Entity entity, AbilityDefinition definition,
        Dictionary<string, object?> queued, PulseContext context)
    {
        // 0a. Rest state: abilities require being awake
        var restState = entity.GetProperty<string>(RestProperties.RestState);
        if (restState == RestProperties.StateSleeping || restState == RestProperties.StateResting)
        {
            return ValidationResult.Asleep;
        }

        // 0b. Alignment range check (must precede resource check per spec)
        if (definition.AlignmentRange != null && _alignmentManager != null)
        {
            var alignment = _alignmentManager.Get(entity.Id);
            if (!definition.AlignmentRange.Allows(alignment))
            {
                return ValidationResult.Alignment_Restricted;
            }
        }

        // 1. Entity has proficiency
        if (!context.ProficiencyManager.HasAbility(entity.Id, definition.Id))
        {
            return ValidationResult.No_Proficiency;
        }

        // 2. Initiate-only check: fizzles if entity is in combat
        if (definition.InitiateOnly && context.CombatManager.IsInCombat(entity.Id))
        {
            return ValidationResult.Initiate_Only;
        }

        // 3. Target validation (for offensive abilities)
        if (IsOffensiveCombatAbility(definition))
        {
            var target = ResolveTarget(entity, queued, context);
            if (target == null)
            {
                return ValidationResult.Invalid_Target;
            }

            // Offensive abilities require combat
            if (!context.CombatManager.IsInCombat(entity.Id))
            {
                return ValidationResult.Not_In_Combat;
            }
        }

        // 4. Effect gate: if ability has an effect, check if it's already active on source
        if (definition.Effect != null)
        {
            if (context.EffectManager.HasEffect(entity.Id, definition.Effect.EffectId))
            {
                return ValidationResult.Effect_Present;
            }
        }

        // 5. Pulse delay
        if (definition.PulseDelay > 0)
        {
            if (_pulseDelays.TryGetValue(entity.Id, out var delays) &&
                delays.TryGetValue(definition.Id, out var nextReady))
            {
                if (context.CurrentPulse < nextReady)
                {
                    return ValidationResult.Pulse_Delay;
                }
            }
        }

        // 6. Resource check (race cast-cost modifier applied)
        var race = GetRaceFor(entity);
        var adjustedCost = RaceCostCalculator.AdjustCost(definition.ResourceCost, race);
        if (adjustedCost > 0)
        {
            var resourceStat = GetResourceForAbility(definition);
            var currentResource = GetCurrentResource(entity, resourceStat);
            if (currentResource < adjustedCost)
            {
                return ValidationResult.Insufficient_Resources;
            }
        }

        return ValidationResult.Ok;
    }

    private void Resolve(Entity entity, AbilityDefinition definition,
        Dictionary<string, object?> queued, PulseContext context)
    {
        // Deduct resources (race cast-cost modifier applied)
        var raceForDeduct = GetRaceFor(entity);
        var deductCost = RaceCostCalculator.AdjustCost(definition.ResourceCost, raceForDeduct);
        if (deductCost > 0)
        {
            var resourceStat = GetResourceForAbility(definition);
            DeductResource(entity, resourceStat, deductCost);
        }

        // Set last_ability_used
        entity.SetProperty(AbilityProperties.LastAbilityUsed, definition.Id);

        // Record pulse delay for next use
        if (definition.PulseDelay > 0)
        {
            if (!_pulseDelays.TryGetValue(entity.Id, out var delays))
            {
                delays = new Dictionary<string, long>();
                _pulseDelays[entity.Id] = delays;
            }
            delays[definition.Id] = context.CurrentPulse + definition.PulseDelay + 1;
        }

        // Proficiency roll (d100 vs proficiency)
        var proficiency = context.ProficiencyManager.GetProficiency(entity.Id, definition.Id) ?? 0;
        var roll = context.Random.Next(1, 101);
        var hit = roll <= proficiency;

        // Resolve target
        var target = ResolveTarget(entity, queued, context);

        if (!hit)
        {
            // Miss — publish miss event, roll proficiency gain
            context.EventBus.Publish(new GameEvent
            {
                Type = "ability.missed",
                SourceEntityId = entity.Id,
                TargetEntityId = target?.Id,
                RoomId = entity.LocationRoomId,
                SourceEntityName = entity.Name,
                Data = new Dictionary<string, object?>
                {
                    ["abilityId"] = definition.Id,
                    ["abilityName"] = definition.Name,
                    ["targetName"] = target?.Name
                }
            });
            context.ProficiencyManager.RollProficiencyGain(entity.Id, definition.Id, context.Random, wasFailure: true);
            return;
        }

        // Hit — apply effect if applicable
        if (definition.Effect != null && target != null)
        {
            var activeEffect = BuildActiveEffect(entity, target, definition, context);
            context.EffectManager.TryApply(activeEffect);
        }

        // Publish ability.used
        context.EventBus.Publish(new GameEvent
        {
            Type = "ability.used",
            SourceEntityId = entity.Id,
            TargetEntityId = target?.Id,
            RoomId = entity.LocationRoomId,
            SourceEntityName = entity.Name,
            Data = new Dictionary<string, object?>
            {
                ["abilityId"] = definition.Id,
                ["abilityName"] = definition.Name,
                ["category"] = definition.Category.ToString().ToLower(),
                ["targetName"] = target?.Name,
                ["handler"] = definition.Handler
            }
        });

        // Roll proficiency gain on hit too
        context.ProficiencyManager.RollProficiencyGain(entity.Id, definition.Id, context.Random);

        // Death check — after handler has sent its output messages
        if (target != null && target.Stats.Hp <= 0)
        {
            context.EventBus.Publish(new GameEvent
            {
                Type = "entity.vital.depleted",
                SourceEntityId = target.Id,
                RoomId = target.LocationRoomId,
                SourceEntityName = target.Name,
                Data = new Dictionary<string, object?>
                {
                    ["vital"] = "hp",
                    ["killerId"] = entity.Id.ToString()
                }
            });
        }
    }

    private Entity? ResolveTarget(Entity entity, Dictionary<string, object?> queued, PulseContext context)
    {
        // Check explicit target in queue entry
        if (queued.TryGetValue("targetEntityId", out var targetObj) && targetObj != null)
        {
            Guid targetId;
            if (targetObj is Guid guid)
            {
                targetId = guid;
            }
            else if (targetObj is string str && Guid.TryParse(str, out var parsed))
            {
                targetId = parsed;
            }
            else
            {
                return null;
            }

            var target = context.World.GetEntity(targetId);
            if (target != null)
            {
                return target;
            }
        }

        // For offensive abilities, retarget to primary combat target
        var abilityId = queued.GetValueOrDefault("abilityId") as string;
        if (abilityId != null)
        {
            var def = context.AbilityRegistry.Get(abilityId);
            if (def != null && IsOffensiveCombatAbility(def))
            {
                var primaryTargetId = context.CombatManager.GetPrimaryTarget(entity.Id);
                if (primaryTargetId.HasValue)
                {
                    return context.World.GetEntity(primaryTargetId.Value);
                }
            }
        }

        // For self-targeted abilities (heals, buffs), target is self
        return entity;
    }

    private static bool IsOffensiveCombatAbility(AbilityDefinition definition)
    {
        // Skills are offensive by default
        if (definition.Category == AbilityCategory.Skill)
        {
            return true;
        }

        // Spells: offensive if no effect block AND has damage_dice in metadata
        if (definition.Category == AbilityCategory.Spell)
        {
            if (definition.Effect != null)
            {
                return false;
            }
            if (definition.Metadata.ContainsKey("heal_dice"))
            {
                return false;
            }
            if (definition.Metadata.ContainsKey("damage_dice"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHealingAbility(AbilityDefinition definition)
    {
        return definition.Metadata.ContainsKey("heal_dice");
    }

    private static ActiveEffect BuildActiveEffect(Entity source, Entity target,
        AbilityDefinition definition, PulseContext context)
    {
        var effectDef = definition.Effect!;
        var modifiers = effectDef.StatModifiers
            .Select(m => new StatModifier($"effect:{effectDef.EffectId}", m.Stat, m.Value))
            .ToList();

        return new ActiveEffect
        {
            Id = effectDef.EffectId,
            SourceAbilityId = definition.Id,
            SourceEntityId = source.Id,
            TargetEntityId = target.Id,
            RemainingPulses = effectDef.DurationPulses,
            StatModifiers = modifiers,
            Flags = new List<string>(effectDef.Flags)
        };
    }

    private static string GetResourceForAbility(AbilityDefinition definition)
    {
        // Skills cost movement, spells cost resource (mana)
        if (definition.Category == AbilityCategory.Skill)
        {
            return "movement";
        }
        return "resource";
    }

    private static int GetCurrentResource(Entity entity, string resourceType)
    {
        if (resourceType == "movement")
        {
            return entity.Stats.Movement;
        }
        return entity.Stats.Resource;
    }

    private static void DeductResource(Entity entity, string resourceType, int cost)
    {
        if (resourceType == "movement")
        {
            entity.Stats.Movement -= cost;
        }
        else
        {
            entity.Stats.Resource -= cost;
        }
    }

    private static void PublishFizzle(Entity entity, string abilityId, string reason, PulseContext context)
    {
        context.EventBus.Publish(new GameEvent
        {
            Type = "ability.fizzled",
            SourceEntityId = entity.Id,
            RoomId = entity.LocationRoomId,
            SourceEntityName = entity.Name,
            Data = new Dictionary<string, object?>
            {
                ["abilityId"] = abilityId,
                ["reason"] = reason
            }
        });
    }
}
