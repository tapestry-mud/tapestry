using Tapestry.Contracts;
using Tapestry.Engine;

namespace Tapestry.Server.Modules;

public class GmcpEventModule : IGameModule
{
    private readonly EventBus _eventBus;
    private readonly World _world;
    private readonly GmcpService _gmcpService;
    private readonly SessionManager _sessions;

    public string Name => "GmcpEvent";

    public GmcpEventModule(
        EventBus eventBus,
        World world,
        GmcpService gmcpService,
        SessionManager sessions)
    {
        _eventBus = eventBus;
        _world = world;
        _gmcpService = gmcpService;
        _sessions = sessions;
    }

    public void Configure()
    {
        _eventBus.Subscribe("player.moved", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            _gmcpService.SendRoomInfoForEntity(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("player.move.failed", evt =>
        {
            Guid entityId;
            if (evt.SourceEntityId.HasValue)
            {
                entityId = evt.SourceEntityId.Value;
            }
            else if (evt.Data.TryGetValue("entityId", out var idObj)
                && Guid.TryParse(idObj?.ToString(), out var parsed))
            {
                entityId = parsed;
            }
            else { return; }
            _gmcpService.SendRoomWrongDir(entityId);
        });

        _eventBus.Subscribe("progression.level.up", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            _gmcpService.SendCharStatus(evt.SourceEntityId.Value);
            _gmcpService.MarkVitalsDirty(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("entity.regen", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            _gmcpService.MarkVitalsDirty(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("entity.vital.depleted", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            _gmcpService.MarkVitalsDirty(evt.SourceEntityId.Value);
        }, priority: -10);

        _eventBus.Subscribe("character.created", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            var session = _sessions.GetByEntityId(evt.SourceEntityId.Value);
            if (session == null) { return; }
            var entity = _world.GetEntity(evt.SourceEntityId.Value);
            if (entity == null) { return; }
            _gmcpService.OnPlayerLoggedIn(session.Connection.Id, entity);
        });
    }
}
