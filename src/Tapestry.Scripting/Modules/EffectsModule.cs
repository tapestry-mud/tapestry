using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Stats;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class EffectsModule : IJintApiModule
{
    private readonly EffectManager _effects;
    private readonly World _world;
    private readonly AbilityRegistry _abilityRegistry;

    public EffectsModule(EffectManager effects, World world, AbilityRegistry abilityRegistry)
    {
        _effects = effects;
        _world = world;
        _abilityRegistry = abilityRegistry;
    }

    public string Namespace => "effects";

    public object Build(JintEngine engine)
    {
        return new
        {
            apply = new Func<string, JsValue, bool>((targetIdStr, definition) =>
            {
                if (!Guid.TryParse(targetIdStr, out var targetId))
                {
                    return false;
                }

                var obj = (ObjectInstance)definition;
                var effectId = obj.Get("id").ToString();

                var durationTicksVal = obj.Get("duration");
                var durationTicks = -1;
                if (durationTicksVal.Type == Types.Number)
                {
                    durationTicks = (int)(double)durationTicksVal.ToObject()!;
                }

                var sourceEntityId = Guid.Empty;
                var sourceVal = obj.Get("source_entity_id");
                if (sourceVal.Type != Types.Undefined && sourceVal.Type != Types.Null)
                {
                    Guid.TryParse(sourceVal.ToString(), out sourceEntityId);
                }

                var statModifiers = new List<StatModifier>();
                var statModsVal = obj.Get("stat_modifiers");
                if (statModsVal is JsArray statModsArray)
                {
                    for (uint i = 0; i < statModsArray.Length; i++)
                    {
                        var elem = statModsArray[i];
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
                            var source = $"effect:{effectId}";
                            statModifiers.Add(new StatModifier(source, statType, modValue));
                        }
                    }
                }

                var flags = new List<string>();
                var flagsVal = obj.Get("flags");
                if (flagsVal is JsArray flagsArray)
                {
                    for (uint i = 0; i < flagsArray.Length; i++)
                    {
                        var elem = flagsArray[i];
                        if (elem.Type != Types.Undefined && elem.Type != Types.Null)
                        {
                            flags.Add(elem.ToString());
                        }
                    }
                }

                var effect = new ActiveEffect
                {
                    Id = effectId,
                    TargetEntityId = targetId,
                    SourceEntityId = sourceEntityId,
                    RemainingPulses = durationTicks,
                    StatModifiers = statModifiers,
                    Flags = flags
                };

                return _effects.TryApply(effect);
            }),

            remove = new Action<string, string>((entityIdStr, effectId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return;
                }

                _effects.Remove(entityId, effectId);
            }),

            removeByFlag = new Action<string, string>((entityIdStr, flag) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return;
                }

                _effects.RemoveByFlag(entityId, flag);
            }),

            hasEffect = new Func<string, string, bool>((entityIdStr, effectId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return false;
                }

                return _effects.HasEffect(entityId, effectId);
            }),

            getActive = new Func<string, object[]>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return Array.Empty<object>();
                }

                var activeEffects = _effects.GetActive(entityId);
                var result = new object[activeEffects.Count];
                for (var i = 0; i < activeEffects.Count; i++)
                {
                    var ae = activeEffects[i];
                    result[i] = new
                    {
                        id = ae.Id,
                        name = _abilityRegistry.Get(ae.SourceAbilityId ?? "")?.Name ?? ae.SourceAbilityId ?? ae.Id,
                        remaining_pulses = ae.RemainingPulses,
                        flags = ae.Flags.ToArray()
                    };
                }
                return result;
            })
        };
    }
}
