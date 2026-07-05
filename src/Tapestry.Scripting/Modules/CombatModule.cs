using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Heartbeat;
using Tapestry.Engine.Registration;
using Tapestry.Engine.Stats;
using Tapestry.Shared;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class CombatModule : IJintApiModule
{
    private readonly CombatManager _combat;
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly GameLoop _gameLoop;
    private readonly EffectManager _effectManager;
    private readonly RegistrationPolicy _registrationPolicy;
    private readonly WindowValidatorRegistry _windowValidators;
    private readonly MobInvocationBudget _invocationBudget;
    private readonly ServerConfig _config;
    private readonly VitalsService _vitalsService;

    public CombatModule(
        CombatManager combat,
        World world,
        EventBus eventBus,
        GameLoop gameLoop,
        EffectManager effectManager,
        RegistrationPolicy registrationPolicy,
        WindowValidatorRegistry windowValidators,
        MobInvocationBudget invocationBudget,
        ServerConfig config,
        VitalsService vitalsService)
    {
        _combat = combat;
        _world = world;
        _eventBus = eventBus;
        _gameLoop = gameLoop;
        _effectManager = effectManager;
        _registrationPolicy = registrationPolicy;
        _windowValidators = windowValidators;
        _invocationBudget = invocationBudget;
        _config = config;
        _vitalsService = vitalsService;
    }

    public string Namespace => "combat";

    public object Build(JintEngine engine)
    {
        return new
        {
            engage = new Func<string, string, string>((attackerIdStr, targetIdStr) =>
            {
                if (!Guid.TryParse(attackerIdStr, out var attackerId) ||
                    !Guid.TryParse(targetIdStr, out var targetId))
                {
                    return "error";
                }

                var attacker = _world.GetEntity(attackerId);
                var target = _world.GetEntity(targetId);

                if (attacker == null || target == null)
                {
                    return "error";
                }

                if (target.HasTag("no_kill"))
                {
                    return "no_kill";
                }

                if (attacker.LocationRoomId != null)
                {
                    var room = _world.GetRoom(attacker.LocationRoomId);
                    if (room != null && room.HasTag("safe"))
                    {
                        return "safe-room";
                    }
                }

                var tick = _gameLoop.TickCount;

                if (_combat.HasFleeCooldown(attackerId, tick))
                {
                    return "flee-cooldown";
                }

                var combatList = _combat.GetCombatList(attackerId);
                if (combatList.Contains(targetId))
                {
                    return "already-fighting";
                }

                var success = _combat.Engage(attacker, target, tick);
                return success ? "ok" : "error";
            }),

            flee = new Func<string, bool>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return false;
                }

                var entity = _world.GetEntity(entityId);
                if (entity == null)
                {
                    return false;
                }

                var tick = _gameLoop.TickCount;
                var context = new PulseContext
                {
                    CurrentTick = tick,
                    CurrentPulse = tick,
                    World = _world,
                    EventBus = _eventBus,
                    CombatManager = _combat,
                    EffectManager = _effectManager,
                    VitalsService = _vitalsService,
                    Random = new Random()
                };

                return _combat.AttemptFlee(entity, context);
            }),

            isInCombat = new Func<string, bool>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return false;
                }

                return _combat.IsInCombat(entityId);
            }),

            removeFromAllCombat = new Action<string>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return;
                }

                _combat.RemoveEntityFromAllCombat(entityId);
            }),

            getCombatants = new Func<string, string[]>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return Array.Empty<string>();
                }

                var combatList = _combat.GetCombatList(entityId);
                var result = new string[combatList.Count];
                for (var i = 0; i < combatList.Count; i++)
                {
                    result[i] = combatList[i].ToString();
                }
                return result;
            }),

            applyDamage = new Action<string, int, string>((entityIdStr, amount, damageType) =>
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

                _vitalsService.Apply(entity, VitalKind.Hp, -amount, "ability.damage");

                // Death check is NOT done here — it happens after the ability handler
                // returns so that the handler's output messages arrive before death messages.
                // AbilityResolutionPhase and ResolveAutoAttacksPhase handle death detection.
            }),

            applyAC = new Func<string, int, string, int>((entityIdStr, rawDamage, damageType) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return rawDamage;
                }

                var entity = _world.GetEntity(entityId);
                if (entity == null)
                {
                    return rawDamage;
                }

                var ac = HitResolver.CalculateArmorClass(entity, damageType);
                var acReduction = ac - 10;
                var reducedDamage = rawDamage - acReduction;

                return Math.Max(1, reducedDamage);
            }),

            formatDamageVerb = new Func<int, string>((damage) =>
            {
                var entry = GetDamageEntry(damage);
                var verbTag = entry.Theme + "_verb";
                if (entry.LeftDecor.Length > 0)
                {
                    return "<" + entry.Theme + ">" + entry.LeftDecor + "</" + entry.Theme + ">"
                        + "<" + verbTag + ">" + entry.Verb + "</" + verbTag + ">"
                        + "<" + entry.Theme + ">" + entry.RightDecor + "</" + entry.Theme + ">";
                }
                else
                {
                    return "<" + verbTag + ">" + entry.Verb + "</" + verbTag + ">";
                }
            }),

            setPrimaryTarget = new Func<string, string, bool>((attackerIdStr, newTargetIdStr) =>
            {
                if (!Guid.TryParse(attackerIdStr, out var attackerId) ||
                    !Guid.TryParse(newTargetIdStr, out var newTargetId))
                {
                    return false;
                }
                return _combat.SetPrimaryTarget(attackerId, newTargetId);
            }),

            savingThrow = new Func<string, string, bool>((entityIdStr, saveType) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return false;
                }

                var entity = _world.GetEntity(entityId);
                if (entity == null)
                {
                    return false;
                }

                var wisdomMod = entity.Stats.Wisdom - 10;
                var savesBonus = 0;
                var savesProp = entity.GetProperty<object>("saves");
                if (savesProp != null)
                {
                    savesBonus = Convert.ToInt32(savesProp);
                }

                var saveTarget = 50 + (wisdomMod * 3) + savesBonus;
                var roll = new Random().Next(1, 101);

                return roll > saveTarget;
            }),

            registerWindow = new Action<string, JsValue>((name, fn) =>
            {
                var packName = engine.CurrentPackOwner();
                var sourceFile = engine.CurrentSourceFile();

                _registrationPolicy.Record(new RegistrationCandidate(
                    Kind: "combat-window",
                    Name: name,
                    Owner: packName,
                    IsOverride: false,
                    Commit: () => _windowValidators.Register(name, ctx =>
                    {
                        var jsCtx = JsValue.FromObject(engine, new
                        {
                            actor = new { id = ctx.Actor.Id.ToString(), hpTier = ctx.Actor.HpTier },
                            target = new { id = ctx.Target.Id.ToString(), hpTier = ctx.Target.HpTier },
                            phase = ctx.Phase,
                            swell = ctx.Swell == null ? (object?)null : new
                            {
                                attackLine = ctx.Swell.AttackLine,
                                requiredCounter = ctx.Swell.RequiredCounter,
                                tell = ctx.Swell.Tell,
                                windowOpen = ctx.Swell.WindowOpen
                            },
                            command = new { verb = ctx.Command.Verb, target = ctx.Command.Target }
                        });

                        JsValue resultJs;
                        using (_invocationBudget.Arm(_config.MobAi.InvocationCapMs))
                        {
                            resultJs = engine.Invoke(fn, jsCtx);
                        }

                        return ReadValidationResult(resultJs);
                    }),
                    SourceFile: sourceFile,
                    Line: 0));
            })
        };
    }

    private static ValidationResult ReadValidationResult(JsValue resultJs)
    {
        if (resultJs is not ObjectInstance obj)
        {
            return new ValidationResult { Outcome = WindowOutcome.Weathered, NarrationKey = "weathered" };
        }

        var outcomeStr = obj.Get("outcome").ToString();
        var outcome = outcomeStr.ToUpperInvariant() switch
        {
            "COUNTERED" => WindowOutcome.Countered,
            "WHIFFED" => WindowOutcome.Whiffed,
            _ => WindowOutcome.Weathered,
        };

        var keyJs = obj.Get("narrationKey");
        var key = keyJs.Type == Types.String ? keyJs.ToString() : outcome.ToString().ToLowerInvariant();

        var qualityJs = obj.Get("quality");
        var quality = qualityJs.Type == Types.String ? qualityJs.ToString() : null;

        return new ValidationResult { Outcome = outcome, Quality = quality, NarrationKey = key };
    }

    private record DamageVerbEntry(int MinDamage, string Verb, string LeftDecor, string RightDecor, string Theme);

    // Design intent (2026-07-04 low-level combat-feel retune, agreed with Travis):
    // verbs key on ABSOLUTE damage - the verb ladder IS the progression channel.
    // A geared level-1 hit (~6-7 damage) reads "grazes"/"hits", a good early roll
    // reads "injures"; the decorated top tiers stay gear/spell territory so
    // late-game power keeps its own vocabulary. The RELATIVE state of the target
    // is a different channel (the condition line in @tapestry/core combat output)
    // - do not fold %-of-target into this table. Boundaries are pinned by
    // DamageVerbLadderTests; retune both together.
    private static readonly DamageVerbEntry[] DamageVerbs =
    {
        new(421, "VAPORIZES",       "<<<---<<<===<<< ", " >>>===>>>--->>>", "dmg_extreme"),
        new(301, "ERADICATES",      "<<--<<--=<<=<<= ", " =>>=>>=>>==-->>-->>", "dmg_extreme"),
        new(241, "PULVERIZES",      "<-<-<-=<=<=<=< ", " >=>=>=>=>->->->-", "dmg_extreme"),
        new(191, "DESTROYS",        "<---=<=<=<=<=<= ", " =>=>=>=>=>==>--->", "dmg_extreme"),
        new(146, "OBLITERATES",     "<---======== ", " ========--->", "dmg_high"),
        new(116, "ANNIHILATES",     "<<-===--===- ", " -===--===->>", "dmg_high"),
        new(91, "MASSACRES",        "<-==-==-== ", " ==-==-==->", "dmg_high"),
        new(73, "DISMEMBERS",       "<=~-~-~-~ ", " ~-~-~-~=>", "dmg_high"),
        new(59, "MUTILATES",        "<~-~-~-~ ", " ~-~-~-~>", "dmg_mid"),
        new(47, "MAIMS",            "~-~-~ ", " ~-~-~", "dmg_mid"),
        new(37, "devastates",       "-=<< ", " >>=-", "dmg_mid"),
        new(29, "decimates",        "-== ", " ==-", "dmg_mid"),
        new(22, "mauls",            "", "", "dmg_low"),
        new(17, "wounds",           "", "", "dmg_low"),
        new(13, "injures",          "", "", "dmg_low"),
        new(9, "hits",              "", "", "dmg_low"),
        new(6, "grazes",            "", "", "dmg_low"),
        new(4, "scratches",         "", "", "dmg_low"),
        new(2, "barely scratches",  "", "", "dmg_low"),
        new(0, "tickles",           "", "", "dmg_low")
    };

    private static DamageVerbEntry GetDamageEntry(int damage)
    {
        foreach (var entry in DamageVerbs)
        {
            if (damage >= entry.MinDamage)
            {
                return entry;
            }
        }
        return DamageVerbs[^1];
    }
}
