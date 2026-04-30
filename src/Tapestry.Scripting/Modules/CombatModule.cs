using Tapestry.Engine;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Heartbeat;
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

    public CombatModule(CombatManager combat, World world, EventBus eventBus, GameLoop gameLoop, EffectManager effectManager)
    {
        _combat = combat;
        _world = world;
        _eventBus = eventBus;
        _gameLoop = gameLoop;
        _effectManager = effectManager;
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

                if (target.HasTag("no-kill") || !target.HasTag("killable"))
                {
                    return "no-kill";
                }

                if (attacker.LocationRoomId != null)
                {
                    var room = _world.GetRoom(attacker.LocationRoomId);
                    if (room != null && (room.HasTag("safe") || room.HasTag("no-combat")))
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

                entity.Stats.Hp -= amount;

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
            })
        };
    }

    private record DamageVerbEntry(int MinDamage, string Verb, string LeftDecor, string RightDecor, string Theme);

    private static readonly DamageVerbEntry[] DamageVerbs =
    {
        new(421, "VAPORIZES",       "<<<---<<<===<<< ", " >>>===>>>--->>>", "dmg_extreme"),
        new(341, "ERADICATES",      "<<--<<--=<<=<<= ", " =>>=>>=>>==-->>-->>", "dmg_extreme"),
        new(281, "PULVERIZES",      "<-<-<-=<=<=<=< ", " >=>=>=>=>->->->-", "dmg_extreme"),
        new(231, "DESTROYS",        "<---=<=<=<=<=<= ", " =>=>=>=>=>==>--->", "dmg_extreme"),
        new(191, "OBLITERATES",     "<---======== ", " ========--->", "dmg_high"),
        new(156, "ANNIHILATES",     "<<-===--===- ", " -===--===->>", "dmg_high"),
        new(126, "MASSACRES",       "<-==-==-== ", " ==-==-==->", "dmg_high"),
        new(101, "DISMEMBERS",      "<=~-~-~-~ ", " ~-~-~-~=>", "dmg_high"),
        new(85, "MUTILATES",        "<~-~-~-~ ", " ~-~-~-~>", "dmg_mid"),
        new(69, "MAIMS",            "~-~-~ ", " ~-~-~", "dmg_mid"),
        new(55, "devastates",       "-=<< ", " >>=-", "dmg_mid"),
        new(43, "decimates",        "-== ", " ==-", "dmg_mid"),
        new(33, "mauls",            "", "", "dmg_low"),
        new(25, "wounds",           "", "", "dmg_low"),
        new(18, "injures",          "", "", "dmg_low"),
        new(13, "hits",             "", "", "dmg_low"),
        new(9, "grazes",            "", "", "dmg_low"),
        new(6, "scratches",         "", "", "dmg_low"),
        new(3, "barely scratches",  "", "", "dmg_low"),
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
