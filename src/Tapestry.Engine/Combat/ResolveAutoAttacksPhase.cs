using Tapestry.Engine.Heartbeat;
using Tapestry.Shared;

namespace Tapestry.Engine.Combat;

public class ResolveAutoAttacksPhase : ICombatPhase
{
    public string Name => "ResolveAutoAttacks";
    public int Priority => 200;

    public void Execute(PulseContext context)
    {
        var combatants = context.CombatManager.GetCombatants()
            .OrderBy(e => e.Type == "player" ? 0 : 1)
            .ToList();

        foreach (var attacker in combatants)
        {
            var targetId = context.CombatManager.GetPrimaryTarget(attacker.Id);
            if (targetId == null)
            {
                continue;
            }

            var target = context.World.GetEntity(targetId.Value);
            if (target == null || target.Stats.Hp <= 0)
            {
                context.CombatManager.RemoveFromCombat(attacker.Id, targetId.Value);
                continue;
            }

            if (attacker.LocationRoomId != target.LocationRoomId)
            {
                context.CombatManager.RemoveFromCombat(attacker.Id, target.Id);
                continue;
            }

            // Determine swing count: 1 base + extra attack passives
            var swingCount = 1;
            swingCount += context.PassiveAbilityProcessor.GetExtraAttackCount(attacker.Id, context.Random);

            for (var swing = 0; swing < swingCount; swing++)
            {
                if (target.Stats.Hp <= 0)
                {
                    break;
                }

                // Check defensive passives on defender
                var defensiveAbility = context.PassiveAbilityProcessor.CheckDefensivePassives(target.Id, context.Random);

                if (defensiveAbility != null)
                {
                    context.EventBus.Publish(new GameEvent
                    {
                        Type = "combat.evade",
                        SourceEntityId = target.Id,
                        TargetEntityId = attacker.Id,
                        RoomId = target.LocationRoomId,
                        Data = new Dictionary<string, object?>
                        {
                            ["defenderName"] = target.Name,
                            ["attackerName"] = attacker.Name,
                            ["abilityId"] = defensiveAbility
                        }
                    });
                    continue;
                }

                var weapon = HitResolver.GetWieldedWeapon(attacker);
                var damageType = HitResolver.GetWeaponDamageType(weapon);
                var hitResult = HitResolver.ResolveHit(attacker, target, weapon, context.Random);

                if (hitResult.IsHit)
                {
                    var damageDice = HitResolver.GetWeaponDamageDice(weapon);
                    var damage = HitResolver.CalculateDamage(attacker, damageDice, context.Random);
                    target.Stats.Hp -= damage;

                    context.EventBus.Publish(new GameEvent
                    {
                        Type = "combat.hit",
                        SourceEntityId = attacker.Id,
                        TargetEntityId = target.Id,
                        RoomId = attacker.LocationRoomId,
                        SourceEntityName = attacker.Name,
                        Data = new Dictionary<string, object?>
                        {
                            ["damage"] = damage,
                            ["damageType"] = damageType,
                            ["weaponName"] = weapon?.GetProperty<string>(CombatProperties.CombatName) ?? "punch",
                            ["attackerName"] = attacker.Name,
                            ["targetName"] = target.Name,
                            ["isCritical"] = hitResult.IsCritical
                        }
                    });

                    if (target.Stats.Hp <= 0)
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
                            ["killerId"] = attacker.Id.ToString()
                        }
                        });
                        break;
                    }
                }
                else
                {
                    context.EventBus.Publish(new GameEvent
                    {
                        Type = "combat.miss",
                        SourceEntityId = attacker.Id,
                        TargetEntityId = target.Id,
                        RoomId = attacker.LocationRoomId,
                        SourceEntityName = attacker.Name,
                        Data = new Dictionary<string, object?>
                        {
                            ["weaponName"] = weapon?.GetProperty<string>(CombatProperties.CombatName) ?? "punch",
                            ["attackerName"] = attacker.Name,
                            ["targetName"] = target.Name,
                            ["isFumble"] = hitResult.IsFumble
                        }
                    });
                }
            }
        }
    }
}
