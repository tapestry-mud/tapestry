using Microsoft.Extensions.Logging;
using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Color;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Login;
using Tapestry.Engine.Persistence;
using Tapestry.Server.Gmcp.Handlers;
using Tapestry.Server.Login;
using Tapestry.Shared;

namespace Tapestry.Server;

public class ConnectionHandler
{
    private readonly SessionManager _sessions;
    private readonly TapestryMetrics _metrics;
    private readonly PlayerPersistenceService _persistence;
    private readonly AccountService _accountService;
    private readonly ServerConfig _config;
    private readonly ILogger<ConnectionHandler> _logger;
    private readonly ILogger<LoginFlow> _loginFlowLogger;
    private readonly FlowEngine _flowEngine;
    private readonly ColorRenderer _colorRenderer;
    private readonly LoginGateRegistry _loginGates;
    private readonly IGmcpConnectionManager _connectionManager;
    private readonly LoginHandler _loginHandler;
    private readonly PlayerSpawner _spawner;

    public ConnectionHandler(
        SessionManager sessions,
        TapestryMetrics metrics,
        PlayerPersistenceService persistence,
        AccountService accountService,
        ServerConfig config,
        ILogger<ConnectionHandler> logger,
        ILogger<LoginFlow> loginFlowLogger,
        FlowEngine flowEngine,
        ColorRenderer colorRenderer,
        LoginGateRegistry loginGates,
        IGmcpConnectionManager connectionManager,
        LoginHandler loginHandler,
        PlayerSpawner spawner)
    {
        _sessions = sessions;
        _metrics = metrics;
        _persistence = persistence;
        _accountService = accountService;
        _config = config;
        _logger = logger;
        _loginFlowLogger = loginFlowLogger;
        _flowEngine = flowEngine;
        _colorRenderer = colorRenderer;
        _loginGates = loginGates;
        _connectionManager = connectionManager;
        _loginHandler = loginHandler;
        _spawner = spawner;
        _flowEngine.NewPlayerEntityFactory = LoginFlow.CreateNewPlayerEntity;
        _flowEngine.GmcpSend = (connectionId, package, payload) =>
        {
            _connectionManager.Send(connectionId, package, payload);
        };
    }

    public void HandleNewConnection(IConnection rawConnection, IGmcpHandler? gmcpHandler)
    {
        if (gmcpHandler != null)
        {
            _connectionManager.RegisterHandler(rawConnection.Id, gmcpHandler);
            rawConnection.OnDisconnected += () => _connectionManager.UnregisterHandler(rawConnection.Id);
        }

        IConnection connection = new ColorRenderingConnection(rawConnection, _colorRenderer);
        var loginContext = new LoginContext(rawConnection.Id, connection);
        _sessions.RegisterPreLogin(loginContext);

        var adapter = new AsyncConnectionAdapter(connection);

        var flow = new LoginFlow(
            adapter, loginContext, _persistence, _accountService, _sessions, _loginGates, _loginHandler, _config,
            _loginFlowLogger, _metrics, _flowEngine);

        _ = Task.Run(async () =>
        {
            try
            {
                await flow.RunAsync(_spawner);
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
