using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Server.Gmcp;

namespace Tapestry.Server.Gmcp.Handlers;

public class CharVitalsHandler : IGmcpPackageHandler
{
    private readonly IGmcpConnectionManager _connectionManager;
    private readonly DirtyVitalsBatcher _batcher;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly EventBus _eventBus;

    public string Name => "CharVitals";
    public IReadOnlyList<string> PackageNames { get; } = new[] { "Char.Vitals" };

    public CharVitalsHandler(
        IGmcpConnectionManager connectionManager,
        DirtyVitalsBatcher batcher,
        SessionManager sessions,
        World world,
        EventBus eventBus)
    {
        _connectionManager = connectionManager;
        _batcher = batcher;
        _sessions = sessions;
        _world = world;
        _eventBus = eventBus;
    }

    public void Configure()
    {
        _batcher.SetFlushCallback(SendVitals);

        _eventBus.Subscribe("ability.used", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            _batcher.MarkDirty(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("entity.regen", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            _batcher.MarkDirty(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("entity.vital.depleted", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            _batcher.MarkDirty(evt.SourceEntityId.Value);
        }, priority: -10);
    }

    public void SendBurst(string connectionId, object entity)
    {
        var e = (Entity)entity;
        _connectionManager.Send(connectionId, "Char.Vitals", BuildPayload(e));
    }

    public void SendVitals(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }
        _connectionManager.Send(entityId, "Char.Vitals", BuildPayload(entity));
    }

    private static object BuildPayload(Entity entity) => new
    {
        hp = entity.Stats.Hp,
        maxhp = entity.Stats.MaxHp,
        mana = entity.Stats.Resource,
        maxmana = entity.Stats.MaxResource,
        mv = entity.Stats.Movement,
        maxmv = entity.Stats.MaxMovement,
    };
}
