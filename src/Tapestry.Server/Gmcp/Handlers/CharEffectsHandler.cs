using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Effects;
using Tapestry.Server.Gmcp;

namespace Tapestry.Server.Gmcp.Handlers;

public class CharEffectsHandler : IGmcpPackageHandler
{
    private readonly IGmcpConnectionManager _connectionManager;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly EffectManager _effectManager;
    private readonly AbilityRegistry _abilityRegistry;

    public string Name => "CharEffects";
    public IReadOnlyList<string> PackageNames { get; } = new[] { "Char.Effects" };

    public CharEffectsHandler(
        IGmcpConnectionManager connectionManager,
        SessionManager sessions,
        World world,
        EventBus eventBus,
        EffectManager effectManager,
        AbilityRegistry abilityRegistry)
    {
        _connectionManager = connectionManager;
        _sessions = sessions;
        _world = world;
        _eventBus = eventBus;
        _effectManager = effectManager;
        _abilityRegistry = abilityRegistry;
    }

    public void Configure()
    {
        _eventBus.Subscribe("effect.applied", evt =>
        {
            if (!evt.TargetEntityId.HasValue) { return; }
            SendEffects(evt.TargetEntityId.Value);
        });

        _eventBus.Subscribe("effect.removed", evt =>
        {
            if (!evt.TargetEntityId.HasValue) { return; }
            SendEffects(evt.TargetEntityId.Value);
        });

        _eventBus.Subscribe("effect.expired", evt =>
        {
            if (!evt.TargetEntityId.HasValue) { return; }
            SendEffects(evt.TargetEntityId.Value);
        });
    }

    public void SendBurst(string connectionId, object entity)
    {
        var e = (Entity)entity;
        _connectionManager.Send(connectionId, "Char.Effects", BuildPayload(e));
    }

    private void SendEffects(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }
        _connectionManager.Send(entityId, "Char.Effects", BuildPayload(entity));
    }

    private object BuildPayload(Entity entity)
    {
        var active = _effectManager.GetActive(entity.Id);
        var effects = active.Select(e => new
        {
            id = e.Id,
            name = _abilityRegistry.Get(e.SourceAbilityId ?? "")?.Name ?? e.SourceAbilityId ?? e.Id,
            remainingPulses = e.RemainingPulses,
            flags = e.Flags,
            type = e.Flags.Contains("harmful") ? "debuff" : "buff",
        }).ToList();

        return new { effects };
    }
}
