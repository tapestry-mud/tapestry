using Microsoft.Extensions.Logging;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Color;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Login;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Server.Login;
using Tapestry.Shared;

namespace Tapestry.Server;

public class ConnectionHandler
{
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly SystemEventQueue _eventQueue;
    private readonly TapestryMetrics _metrics;
    private readonly PlayerPersistenceService _persistence;
    private readonly ServerConfig _config;
    private readonly ILogger<ConnectionHandler> _logger;
    private readonly ILogger<LoginFlow> _loginFlowLogger;
    private readonly ILogger<PlayerSpawner> _spawnerLogger;
    private readonly FlowEngine _flowEngine;
    private readonly ColorRenderer _colorRenderer;
    private readonly LoginGateRegistry _loginGates;
    private readonly GmcpService _gmcpService;
    private readonly GameLoop _gameLoop;
    private readonly MobAIManager _mobAI;

    public ConnectionHandler(
        SessionManager sessions,
        World world,
        SystemEventQueue eventQueue,
        TapestryMetrics metrics,
        PlayerPersistenceService persistence,
        ServerConfig config,
        ILogger<ConnectionHandler> logger,
        ILogger<LoginFlow> loginFlowLogger,
        ILogger<PlayerSpawner> spawnerLogger,
        FlowEngine flowEngine,
        ColorRenderer colorRenderer,
        LoginGateRegistry loginGates,
        GmcpService gmcpService,
        GameLoop gameLoop,
        MobAIManager mobAI)
    {
        _sessions = sessions;
        _world = world;
        _eventQueue = eventQueue;
        _metrics = metrics;
        _persistence = persistence;
        _config = config;
        _logger = logger;
        _loginFlowLogger = loginFlowLogger;
        _spawnerLogger = spawnerLogger;
        _flowEngine = flowEngine;
        _colorRenderer = colorRenderer;
        _loginGates = loginGates;
        _gmcpService = gmcpService;
        _gameLoop = gameLoop;
        _mobAI = mobAI;
        _flowEngine.NewPlayerEntityFactory = LoginFlow.CreateNewPlayerEntity;
        _flowEngine.GmcpSend = (connectionId, package, payload) =>
        {
            _gmcpService.SendRaw(connectionId, package, payload);
        };
    }

    public void HandleNewConnection(IConnection rawConnection, IGmcpHandler? gmcpHandler)
    {
        if (gmcpHandler != null)
        {
            _gmcpService.RegisterHandler(rawConnection.Id, gmcpHandler);
            rawConnection.OnDisconnected += () => _gmcpService.UnregisterHandler(rawConnection.Id);
        }

        IConnection connection = new ColorRenderingConnection(rawConnection, _colorRenderer);
        var loginContext = new LoginContext(rawConnection.Id, connection);
        _sessions.RegisterPreLogin(loginContext);

        var adapter = new AsyncConnectionAdapter(connection);

        var spawner = new PlayerSpawner(
            _sessions, _world, _gameLoop, _gmcpService, _mobAI, _eventQueue, _metrics, _spawnerLogger);

        var flow = new LoginFlow(
            adapter, loginContext, _persistence, _sessions, _loginGates, _gmcpService, _config,
            _loginFlowLogger, _metrics, _flowEngine);

        _ = Task.Run(async () =>
        {
            try
            {
                await flow.RunAsync(spawner);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in login flow for {Id}", rawConnection.Id);
                _sessions.RemovePreLogin(rawConnection.Id);
                if (rawConnection.IsConnected)
                {
                    rawConnection.Disconnect("internal error");
                }
            }
        });
    }
}
