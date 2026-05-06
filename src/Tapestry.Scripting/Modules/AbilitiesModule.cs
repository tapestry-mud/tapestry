using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Stats;
using Tapestry.Shared;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class AbilitiesModule : IJintApiModule
{
    private readonly AbilityRegistry _registry;
    private readonly ProficiencyManager _proficiency;
    private readonly World _world;
    private readonly GameLoop _gameLoop;
    private readonly EventBus _eventBus;
    private readonly AlignmentConfig _alignmentConfig;

    public AbilitiesModule(AbilityRegistry registry, ProficiencyManager proficiency, World world, GameLoop gameLoop, EventBus eventBus, AlignmentConfig alignmentConfig)
    {
        _registry = registry;
        _proficiency = proficiency;
        _world = world;
        _gameLoop = gameLoop;
        _eventBus = eventBus;
        _alignmentConfig = alignmentConfig;
    }

    public string Namespace => "abilities";

    private static object BuildEntityObject(Entity entity)
    {
        return new
        {
            entityId = entity.Id.ToString(),
            name = entity.Name,
            roomId = entity.LocationRoomId ?? "",
            stats = new
            {
                strength = entity.Stats.Strength,
                intelligence = entity.Stats.Intelligence,
                wisdom = entity.Stats.Wisdom,
                dexterity = entity.Stats.Dexterity,
                constitution = entity.Stats.Constitution,
                luck = entity.Stats.Luck,
                hp = entity.Stats.Hp,
                max_hp = entity.Stats.MaxHp,
                resource = entity.Stats.Resource,
                max_resource = entity.Stats.MaxResource,
                movement = entity.Stats.Movement,
                max_movement = entity.Stats.MaxMovement,
                damage_roll = entity.GetProperty<int>("damage_roll"),
                hit_roll = entity.GetProperty<int>("hit_roll"),
                channeling_damage = entity.GetProperty<int>("channeling_damage"),
                channeling_protection = entity.GetProperty<int>("channeling_protection")
            }
        };
    }

    public object Build(JintEngine engine)
    {
        _eventBus.Subscribe("ability.used", (evt) =>
        {
            if (!evt.Data.TryGetValue("handler", out var handlerObj) || handlerObj is not JsValue abilityHandler || abilityHandler.Type == Types.Undefined || abilityHandler.Type == Types.Null)
            {
                return;
            }

            var sourceEntity = evt.SourceEntityId.HasValue ? _world.GetEntity(evt.SourceEntityId.Value) : null;
            var targetEntity = evt.TargetEntityId.HasValue ? _world.GetEntity(evt.TargetEntityId.Value) : null;

            if (sourceEntity == null)
            {
                return;
            }

            var user = BuildEntityObject(sourceEntity);

            var target = (targetEntity != null && targetEntity.Id != sourceEntity.Id)
                ? BuildEntityObject(targetEntity)
                : user;

            var context = new {};

            try
            {
                engine.Invoke(abilityHandler, null, new object[] { user, target, context });
            }
            catch (Exception ex)
            {
                var abilityId = evt.Data.TryGetValue("abilityId", out var aid) ? aid?.ToString() : "unknown";
                Console.Error.WriteLine($"[Ability Handler Error] {abilityId}: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
            }
        });

        return new
        {
            register = new Action<JsValue>((definition) =>
            {
                var obj = (ObjectInstance)definition;
                var id = obj.Get("id").ToString();
                var name = obj.Get("name").ToString();

                var typeVal = obj.Get("type");
                var abilityType = AbilityType.Active;
                if (typeVal.Type != Types.Undefined && typeVal.Type != Types.Null)
                {
                    Enum.TryParse<AbilityType>(typeVal.ToString(), true, out abilityType);
                }

                var categoryVal = obj.Get("category");
                var category = AbilityCategory.Skill;
                if (categoryVal.Type != Types.Undefined && categoryVal.Type != Types.Null)
                {
                    Enum.TryParse<AbilityCategory>(categoryVal.ToString(), true, out category);
                }

                var resourceCostVal = obj.Get("resource_cost");
                var resourceCost = 0;
                if (resourceCostVal.Type == Types.Number)
                {
                    resourceCost = (int)(double)resourceCostVal.ToObject()!;
                }

                var profGainChanceVal = obj.Get("proficiency_gain_chance");
                var proficiencyGainChance = 0.05;
                if (profGainChanceVal.Type == Types.Number)
                {
                    proficiencyGainChance = (double)profGainChanceVal.ToObject()!;
                }

                var failureGainMultiplierVal = obj.Get("failure_gain_multiplier");
                var failureGainMultiplier = 0.25;
                if (failureGainMultiplierVal.Type == Types.Number)
                {
                    failureGainMultiplier = (double)failureGainMultiplierVal.ToObject()!;
                }

                var priorityVal = obj.Get("priority");
                var priority = 0;
                if (priorityVal.Type == Types.Number)
                {
                    priority = (int)(double)priorityVal.ToObject()!;
                }

                var packNameVal = engine.GetValue("__currentPack");
                var packNameStr = (packNameVal.Type != Types.Undefined && packNameVal.Type != Types.Null)
                    ? packNameVal.ToString()
                    : "";

                var sourceFileVal = engine.GetValue("__currentSource");
                var sourceFileStr = (sourceFileVal.Type != Types.Undefined && sourceFileVal.Type != Types.Null)
                    ? sourceFileVal.ToString()
                    : "";

                var shortNameVal = obj.Get("short_name");
                var shortName = (shortNameVal.Type != Types.Undefined && shortNameVal.Type != Types.Null)
                    ? shortNameVal.ToString()
                    : null;

                var handler = obj.Get("handler");
                object? handlerObj = (handler.Type == Types.Undefined) ? null : (object)handler;

                var metadata = new Dictionary<string, object?>();
                var metadataVal = obj.Get("metadata");
                if (metadataVal.Type != Types.Undefined && metadataVal.Type != Types.Null && metadataVal is ObjectInstance metaObj)
                {
                    foreach (var prop in metaObj.GetOwnProperties())
                    {
                        var val = prop.Value.Value;
                        var keyStr = prop.Key.ToString();
                        if (val.Type == Types.String)
                        {
                            metadata[keyStr] = val.ToString();
                        }
                        else if (val.Type == Types.Number)
                        {
                            metadata[keyStr] = (int)(double)val.ToObject()!;
                        }
                        else if (val.Type == Types.Boolean)
                        {
                            metadata[keyStr] = (bool)val.ToObject()!;
                        }
                        else
                        {
                            metadata[keyStr] = val.Type == Types.Null ? null : val.ToString();
                        }
                    }
                }

                var pulseDelayVal = obj.Get("pulse_delay");
                var pulseDelay = 0;
                if (pulseDelayVal.Type == Types.Number)
                {
                    pulseDelay = (int)(double)pulseDelayVal.ToObject()!;
                }

                var initiateOnlyVal = obj.Get("initiate_only");
                var initiateOnly = false;
                if (initiateOnlyVal.Type == Types.Boolean)
                {
                    initiateOnly = (bool)initiateOnlyVal.ToObject()!;
                }

                var maxChanceVal = obj.Get("max_chance");
                var maxChance = 100;
                if (maxChanceVal.Type == Types.Number)
                {
                    maxChance = (int)(double)maxChanceVal.ToObject()!;
                }

                var varianceVal = obj.Get("variance");
                var variance = 100;
                if (varianceVal.Type == Types.Number)
                {
                    variance = (int)(double)varianceVal.ToObject()!;
                }

                var gainStatVal = obj.Get("gain_stat");
                string? gainStat = (gainStatVal.Type != Types.Undefined && gainStatVal.Type != Types.Null)
                    ? gainStatVal.ToString()
                    : null;

                var gainStatScaleVal = obj.Get("gain_stat_scale");
                var gainStatScale = 0.0;
                if (gainStatScaleVal.Type == Types.Number)
                {
                    gainStatScale = (double)gainStatScaleVal.ToObject()!;
                }

                var requiresSlotVal = obj.Get("requires_slot");
                string? requiresSlot = (requiresSlotVal.Type != Types.Undefined && requiresSlotVal.Type != Types.Null)
                    ? requiresSlotVal.ToString()
                    : null;

                var requiresSlotTagVal = obj.Get("requires_slot_tag");
                string? requiresSlotTag = (requiresSlotTagVal.Type != Types.Undefined && requiresSlotTagVal.Type != Types.Null)
                    ? requiresSlotTagVal.ToString()
                    : null;

                AlignmentRange? alignmentRange = null;
                var alignmentRangeVal = obj.Get("alignment_range");
                if (alignmentRangeVal.Type != Types.Undefined && alignmentRangeVal.Type != Types.Null
                    && alignmentRangeVal is ObjectInstance arObj)
                {
                    int? min = null, max = null;

                    var bucketsVal = arObj.Get("buckets");
                    if (bucketsVal is JsArray bArr)
                    {
                        var buckets = new List<string>();
                        for (uint i = 0; i < bArr.Length; i++)
                        {
                            if (bArr[i].Type != Types.Undefined) { buckets.Add(bArr[i].ToString()); }
                        }
                        (min, max) = _alignmentConfig.ResolveBuckets(buckets);
                    }
                    else
                    {
                        var minVal = arObj.Get("min");
                        if (minVal.Type == Types.Number) { min = (int)(double)minVal.ToObject()!; }
                        var maxVal = arObj.Get("max");
                        if (maxVal.Type == Types.Number) { max = (int)(double)maxVal.ToObject()!; }
                    }

                    alignmentRange = new AlignmentRange { Min = min, Max = max };
                }

                var canTarget = new List<string>();
                var canTargetVal = obj.Get("can_target");
                if (canTargetVal is JsArray canTargetArray)
                {
                    for (uint ci = 0; ci < canTargetArray.Length; ci++)
                    {
                        var elem = canTargetArray[ci];
                        if (elem.Type != Types.Undefined && elem.Type != Types.Null)
                        {
                            canTarget.Add(elem.ToString());
                        }
                    }
                }

                AbilityEffectDefinition? effectDef = null;
                var effectVal = obj.Get("effect");
                if (effectVal.Type != Types.Undefined && effectVal.Type != Types.Null && effectVal is ObjectInstance effectObj)
                {
                    var effectId = effectObj.Get("effect_id").ToString();

                    var durationPulsesVal = effectObj.Get("duration");
                    var durationPulses = 0;
                    if (durationPulsesVal.Type == Types.Number)
                    {
                        durationPulses = (int)(double)durationPulsesVal.ToObject()!;
                    }

                    var effectFlags = new List<string>();
                    var effectFlagsVal = effectObj.Get("flags");
                    if (effectFlagsVal is JsArray effectFlagsArray)
                    {
                        for (uint fi = 0; fi < effectFlagsArray.Length; fi++)
                        {
                            var flagElem = effectFlagsArray[fi];
                            if (flagElem.Type != Types.Undefined && flagElem.Type != Types.Null)
                            {
                                effectFlags.Add(flagElem.ToString());
                            }
                        }
                    }

                    var statModDefs = new List<StatModifierDefinition>();
                    var statModsVal = effectObj.Get("stat_modifiers");
                    if (statModsVal is JsArray statModsArray)
                    {
                        for (uint si = 0; si < statModsArray.Length; si++)
                        {
                            var elem = statModsArray[si];
                            if (elem.Type == Types.Undefined || elem.Type == Types.Null)
                            {
                                continue;
                            }

                            var elemObj = (ObjectInstance)elem;
                            var statStr = elemObj.Get("stat").ToString();
                            var valueVal = elemObj.Get("value");
                            var modValue = valueVal.Type == Types.Number ? (int)(double)valueVal.ToObject()! : 0;

                            if (Enum.TryParse<StatType>(statStr, true, out var statType))
                            {
                                statModDefs.Add(new StatModifierDefinition
                                {
                                    Stat = statType,
                                    Value = modValue
                                });
                            }
                        }
                    }

                    effectDef = new AbilityEffectDefinition
                    {
                        EffectId = effectId,
                        DurationPulses = durationPulses,
                        Flags = effectFlags,
                        StatModifiers = statModDefs
                    };
                }

                var abilityDef = new AbilityDefinition
                {
                    Id = id,
                    Name = name,
                    Type = abilityType,
                    Category = category,
                    ResourceCost = resourceCost,
                    PulseDelay = pulseDelay,
                    InitiateOnly = initiateOnly,
                    MaxChance = maxChance,
                    ProficiencyGainChance = proficiencyGainChance,
                    FailureProficiencyGainMultiplier = failureGainMultiplier,
                    Priority = priority,
                    PackName = packNameStr,
                    ShortName = shortName,
                    SourceFile = sourceFileStr,
                    Effect = effectDef,
                    CanTarget = canTarget,
                    Metadata = metadata,
                    Handler = handlerObj,
                    AlignmentRange = alignmentRange,
                    Variance = variance,
                    GainStat = gainStat,
                    GainStatScale = gainStatScale,
                    RequiresSlot = requiresSlot,
                    RequiresSlotTag = requiresSlotTag
                };

                _registry.Register(abilityDef);
            }),

            learn = new Action<string, string, JsValue>((entityIdStr, abilityId, options) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return;
                }

                var initialProficiency = 1;
                if (options.Type != Types.Undefined && options.Type != Types.Null && options is ObjectInstance optObj)
                {
                    var profVal = optObj.Get("proficiency");
                    if (profVal.Type == Types.Number)
                    {
                        initialProficiency = (int)(double)profVal.ToObject()!;
                    }
                }

                _proficiency.Learn(entityId, abilityId, initialProficiency);
            }),

            forget = new Action<string, string>((entityIdStr, abilityId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return;
                }

                _proficiency.Forget(entityId, abilityId);
            }),

            getProficiency = new Func<string, string, object?>((entityIdStr, abilityId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return null;
                }

                return _proficiency.GetProficiency(entityId, abilityId);
            }),

            setProficiency = new Action<string, string, int>((entityIdStr, abilityId, value) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return;
                }

                _proficiency.SetProficiency(entityId, abilityId, value);
            }),

            increaseProficiency = new Action<string, string, int, int>((entityIdStr, abilityId, amount, cap) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return;
                }

                _proficiency.IncreaseProficiency(entityId, abilityId, amount, cap);
            }),

            getLearnedAbilities = new Func<string, object[]>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return Array.Empty<object>();
                }

                var learned = _proficiency.GetLearnedAbilities(entityId);
                var result = new object[learned.Count];
                for (var i = 0; i < learned.Count; i++)
                {
                    result[i] = new
                    {
                        id = learned[i].AbilityId,
                        proficiency = learned[i].Proficiency
                    };
                }
                return result;
            }),

            getDefinition = new Func<string, object?>((abilityId) =>
            {
                var def = _registry.Get(abilityId);
                if (def == null)
                {
                    return null;
                }
                return new
                {
                    id = def.Id,
                    name = def.Name,
                    short_name = def.ShortName,
                    type = def.Type == AbilityType.Active ? "active" : "passive",
                    category = def.Category == AbilityCategory.Skill ? "skill" : "spell",
                    resource_cost = def.ResourceCost,
                    pulse_delay = def.PulseDelay,
                    initiate_only = def.InitiateOnly,
                    max_chance = def.MaxChance,
                    has_effect = def.Effect != null,
                    can_target = def.CanTarget.ToArray()
                };
            }),

            getAll = new Func<object[]>(() =>
            {
                return _registry.GetAll()
                    .Select(def => (object)new
                    {
                        id = def.Id,
                        name = def.Name,
                        type = def.Type == AbilityType.Active ? "active" : "passive",
                        category = def.Category == AbilityCategory.Skill ? "skill" : "spell"
                    })
                    .ToArray();
            }),

            queue = new Action<string, string, string>((entityIdStr, abilityId, targetIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return;
                }

                var entity = _world.GetEntity(entityId);
                if (entity == null)
                {
                    return;
                }

                var currentQueue = entity.GetProperty<List<object>>(AbilityProperties.QueuedActions) ?? new List<object>();
                currentQueue.Add(new Dictionary<string, object?>
                {
                    ["abilityId"] = abilityId,
                    ["targetEntityId"] = targetIdStr
                });
                entity.SetProperty(AbilityProperties.QueuedActions, currentQueue);
            }),

            repeatLast = new Action<string>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return;
                }

                var entity = _world.GetEntity(entityId);
                if (entity == null)
                {
                    return;
                }

                var lastAbilityId = entity.GetProperty<string>(AbilityProperties.LastAbilityUsed);
                if (lastAbilityId == null)
                {
                    return;
                }

                var currentQueue = entity.GetProperty<List<object>>(AbilityProperties.QueuedActions) ?? new List<object>();
                currentQueue.Add(new Dictionary<string, object?>
                {
                    ["abilityId"] = lastAbilityId,
                    ["targetEntityId"] = (object?)null
                });
                entity.SetProperty(AbilityProperties.QueuedActions, currentQueue);
            }),

            getQueue = new Func<string, object[]>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return Array.Empty<object>();
                }

                var entity = _world.GetEntity(entityId);
                if (entity == null)
                {
                    return Array.Empty<object>();
                }

                var currentQueue = entity.GetProperty<List<object>>(AbilityProperties.QueuedActions);
                if (currentQueue == null || currentQueue.Count == 0)
                {
                    return Array.Empty<object>();
                }

                var result = new object[currentQueue.Count];
                for (var i = 0; i < currentQueue.Count; i++)
                {
                    var entry = currentQueue[i] as Dictionary<string, object?>;
                    if (entry == null)
                    {
                        result[i] = new { abilityId = (string?)null, targetEntityId = (string?)null };
                        continue;
                    }

                    entry.TryGetValue("abilityId", out var abilityIdObj);
                    entry.TryGetValue("targetEntityId", out var targetIdObj);
                    result[i] = new
                    {
                        abilityId = abilityIdObj?.ToString(),
                        targetEntityId = targetIdObj?.ToString()
                    };
                }
                return result;
            }),

            clearQueue = new Action<string>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return;
                }

                var entity = _world.GetEntity(entityId);
                if (entity == null)
                {
                    return;
                }

                entity.SetProperty(AbilityProperties.QueuedActions, null);
            })

            // isOnCooldown removed — replaced by pulse-based heartbeat system (Task 5+).
            // getCooldownRemaining removed — replaced by pulse-based heartbeat system (Task 5+).
        };
    }
}
