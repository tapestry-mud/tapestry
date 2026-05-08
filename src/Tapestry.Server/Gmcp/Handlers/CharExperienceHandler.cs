using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Progression;
using Tapestry.Server.Gmcp;

namespace Tapestry.Server.Gmcp.Handlers;

public class CharExperienceHandler : IGmcpPackageHandler
{
    private readonly IGmcpConnectionManager _connectionManager;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly ProgressionManager _progression;

    public string Name => "CharExperience";
    public IReadOnlyList<string> PackageNames { get; } = new[] { "Char.Experience" };

    public CharExperienceHandler(
        IGmcpConnectionManager connectionManager,
        SessionManager sessions,
        World world,
        EventBus eventBus,
        ProgressionManager progression)
    {
        _connectionManager = connectionManager;
        _sessions = sessions;
        _world = world;
        _eventBus = eventBus;
        _progression = progression;
    }

    public void Configure()
    {
        _eventBus.Subscribe("progression.xp.gained", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendExperience(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("progression.level.up", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendExperience(evt.SourceEntityId.Value);
        });
    }

    public void SendBurst(string connectionId, object entity)
    {
        var e = (Entity)entity;
        _connectionManager.Send(connectionId, "Char.Experience", BuildPayload(e));
    }

    private void SendExperience(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }
        _connectionManager.Send(entityId, "Char.Experience", BuildPayload(entity));
    }

    private object BuildPayload(Entity entity)
    {
        var tracks = _progression.GetAllTracks()
            .Select(t =>
            {
                var info = _progression.GetTrackInfo(entity.Id, t.Name);
                if (info == null) { return null; }
                return new
                {
                    name = t.Name,
                    level = info.Level,
                    xp = info.Xp,
                    xpToNext = info.XpToNext,
                    currentLevelThreshold = info.CurrentLevelThreshold,
                };
            })
            .Where(t => t != null)
            .ToList();

        return new { tracks };
    }
}
