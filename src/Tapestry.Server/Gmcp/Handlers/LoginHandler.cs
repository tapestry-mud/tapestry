using Tapestry.Contracts;
using Tapestry.Engine;
using Tapestry.Server.Gmcp;

namespace Tapestry.Server.Gmcp.Handlers;

public class LoginHandler : IGmcpPackageHandler
{
    private readonly IGmcpConnectionManager _connectionManager;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly PostLoginOrchestrator _orchestrator;

    public string Name => "Login";
    public IReadOnlyList<string> PackageNames { get; } =
        new[] { "Char.Login.Phase", "Login.Prompt", "Flow.Step", "Flow.Help" };

    public LoginHandler(
        IGmcpConnectionManager connectionManager,
        SessionManager sessions,
        World world,
        EventBus eventBus,
        PostLoginOrchestrator orchestrator)
    {
        _connectionManager = connectionManager;
        _sessions = sessions;
        _world = world;
        _eventBus = eventBus;
        _orchestrator = orchestrator;
    }

    public void Configure()
    {
        _eventBus.Subscribe("character.created", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            var session = _sessions.GetByEntityId(evt.SourceEntityId.Value);
            if (session == null) { return; }
            var entity = _world.GetEntity(evt.SourceEntityId.Value);
            if (entity == null) { return; }
            SendLoginPhase(session.Connection.Id, "playing");
            _orchestrator.SendPostLoginBurst(session.Connection.Id, entity);
        });
    }

    public void SendBurst(string connectionId, object entity) { }

    public void SendLoginPhase(string connectionId, string phase)
    {
        _connectionManager.Send(connectionId, "Char.Login.Phase", new { phase });
    }

    public void SendLoginPrompt(string connectionId, string prompt)
    {
        _connectionManager.Send(connectionId, "Login.Prompt", new { prompt });
    }

    public void TriggerPostLoginBurst(string connectionId, Entity entity)
    {
        SendLoginPhase(connectionId, "playing");
        _orchestrator.SendPostLoginBurst(connectionId, entity);
    }
}
