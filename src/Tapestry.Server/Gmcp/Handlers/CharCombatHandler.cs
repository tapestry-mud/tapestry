using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Combat;
using Tapestry.Server.Gmcp;

namespace Tapestry.Server.Gmcp.Handlers;

public class CharCombatHandler : IGmcpPackageHandler
{
    private readonly IGmcpConnectionManager _connectionManager;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly CombatManager _combatManager;

    public string Name => "CharCombat";
    public IReadOnlyList<string> PackageNames { get; } = new[] { "Char.Combat.Target", "Char.Combat.Targets" };

    public CharCombatHandler(
        IGmcpConnectionManager connectionManager,
        SessionManager sessions,
        World world,
        EventBus eventBus,
        CombatManager combatManager)
    {
        _connectionManager = connectionManager;
        _sessions = sessions;
        _world = world;
        _eventBus = eventBus;
        _combatManager = combatManager;
    }

    public void Configure()
    {
        _eventBus.Subscribe("combat.engage", evt =>
        {
            if (evt.SourceEntityId.HasValue)
            {
                SendCombatTarget(evt.SourceEntityId.Value);
                SendCombatTargets(evt.SourceEntityId.Value);
            }
            if (evt.TargetEntityId.HasValue)
            {
                SendCombatTarget(evt.TargetEntityId.Value);
                SendCombatTargets(evt.TargetEntityId.Value);
            }
        });

        _eventBus.Subscribe("combat.hit", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendCombatTarget(evt.SourceEntityId.Value);
            SendCombatTargets(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("combat.end", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendCombatTarget(evt.SourceEntityId.Value);
            SendCombatTargets(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("combat.kill", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendCombatTarget(evt.SourceEntityId.Value);
            SendCombatTargets(evt.SourceEntityId.Value);
        });
    }

    public void SendBurst(string connectionId, object entity) { }

    private void SendCombatTarget(Guid entityId)
    {
        var targetId = _combatManager.GetPrimaryTarget(entityId);
        if (targetId == null)
        {
            _connectionManager.Send(entityId, "Char.Combat.Target", new { active = false });
            return;
        }

        var target = _world.GetEntity(targetId.Value);
        if (target == null)
        {
            _connectionManager.Send(entityId, "Char.Combat.Target", new { active = false });
            return;
        }

        var tier = HealthTier.Get(target.Stats.Hp, target.Stats.MaxHp);
        _connectionManager.Send(entityId, "Char.Combat.Target", new
        {
            active = true,
            name = target.Name,
            healthTier = tier.Tier,
            healthText = tier.Text,
        });
    }

    private void SendCombatTargets(Guid entityId)
    {
        var combatList = _combatManager.GetCombatList(entityId);
        if (combatList.Count == 0)
        {
            _connectionManager.Send(entityId, "Char.Combat.Targets", new { targets = Array.Empty<object>() });
            return;
        }

        var primaryId = _combatManager.GetPrimaryTarget(entityId);
        var targets = combatList
            .Select(id => _world.GetEntity(id))
            .Where(e => e != null)
            .Select(e =>
            {
                var t = HealthTier.Get(e!.Stats.Hp, e.Stats.MaxHp);
                return new
                {
                    id = e.Id.ToString(),
                    name = e.Name,
                    healthTier = t.Tier,
                    healthText = t.Text,
                    isPrimary = e.Id == primaryId,
                };
            })
            .ToList();

        _connectionManager.Send(entityId, "Char.Combat.Targets", new { targets });
    }
}
