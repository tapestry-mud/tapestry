using Tapestry.Contracts;
using Tapestry.Engine;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Heartbeat;
using Tapestry.Shared;

namespace Tapestry.Server.Modules;

public class CombatEventModule : IGameModule
{
    private readonly EventBus _eventBus;
    private readonly World _world;
    private readonly CombatManager _combat;
    private readonly GameLoop _gameLoop;
    private readonly AbilityResolutionPhase _abilityResolutionPhase;

    public string Name => "CombatEvent";

    public CombatEventModule(
        EventBus eventBus,
        World world,
        CombatManager combat,
        GameLoop gameLoop,
        AbilityResolutionPhase abilityResolutionPhase)
    {
        _eventBus = eventBus;
        _world = world;
        _combat = combat;
        _gameLoop = gameLoop;
        _abilityResolutionPhase = abilityResolutionPhase;
    }

    public void Configure()
    {
        WireAggroCombat();
        WireDeathHandling();
        WirePulseDelayClearing();
    }

    private void WireAggroCombat()
    {
        _eventBus.Subscribe("mob.aggro", (evt) =>
        {
            if (evt.Data.TryGetValue("attackerId", out var atkIdObj) &&
                evt.Data.TryGetValue("targetId", out var tgtIdObj))
            {
                var attackerId = Guid.Parse(atkIdObj?.ToString() ?? "");
                var targetId = Guid.Parse(tgtIdObj?.ToString() ?? "");
                var attacker = _world.GetEntity(attackerId);
                var target = _world.GetEntity(targetId);
                if (attacker != null && target != null)
                {
                    _combat.Engage(attacker, target, _gameLoop.TickCount);
                }
            }
        });
    }

    private void WireDeathHandling()
    {
        _eventBus.Subscribe("entity.vital.depleted", (evt) =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            if (!evt.Data.TryGetValue("vital", out var vital) || vital?.ToString() != "hp") { return; }

            var victimId = evt.SourceEntityId.Value;

            // Resolve killer: prefer explicit attackerId from event (ability one-shots),
            // fall back to combat primary target (auto-attack kills).
            Guid? killerId = null;
            if (evt.Data.TryGetValue("attackerId", out var attackerIdStr)
                && Guid.TryParse(attackerIdStr?.ToString(), out var parsedAttackerId))
            {
                killerId = parsedAttackerId;
            }
            else
            {
                killerId = _combat.GetPrimaryTarget(victimId);
            }

            // Fire cancellable death check -- pack skills (e.g. cockroach) subscribe here
            // and set cancel = true to intercept the death and handle resurrection instead.
            var deathCheck = new GameEvent
            {
                Type = "entity.death.check",
                SourceEntityId = victimId,
                TargetEntityId = killerId,
                RoomId = _world.GetEntity(victimId)?.LocationRoomId,
                Data = new Dictionary<string, object?>
                {
                    ["victimId"] = victimId.ToString(),
                    ["killerId"] = killerId?.ToString(),
                    ["cancel"] = (object)false
                }
            };
            _eventBus.Publish(deathCheck);

            if (deathCheck.Data["cancel"] is true) { return; }

            _combat.HandleEntityDeath(victimId, killerId);
        }, priority: 100);
    }

    private void WirePulseDelayClearing()
    {
        _eventBus.Subscribe("combat.flee", evt =>
        {
            if (evt.SourceEntityId.HasValue)
            {
                _abilityResolutionPhase.ClearPulseDelays(evt.SourceEntityId.Value);
            }
        });

        _eventBus.Subscribe("combat.end", evt =>
        {
            if (evt.SourceEntityId.HasValue)
            {
                _abilityResolutionPhase.ClearPulseDelays(evt.SourceEntityId.Value);
            }
        });
    }
}
