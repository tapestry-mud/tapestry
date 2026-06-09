using Microsoft.Extensions.Logging;
using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Color;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Login;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Text;
using Tapestry.Server.Gmcp.Handlers;
using Tapestry.Engine.Watch;
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
    private readonly OutputWrapper _outputWrapper;
    private readonly OutputWidthService _outputWidthService;
    private readonly LoginGateRegistry _loginGates;
    private readonly IGmcpConnectionManager _connectionManager;
    private readonly LoginHandler _loginHandler;
    private readonly PlayerSpawner _spawner;
    private readonly WatchRegistry _watchRegistry;

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
        OutputWrapper outputWrapper,
        OutputWidthService outputWidthService,
        LoginGateRegistry loginGates,
        IGmcpConnectionManager connectionManager,
        LoginHandler loginHandler,
        PlayerSpawner spawner,
        WatchRegistry watchRegistry)
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
        _outputWrapper = outputWrapper;
        _outputWidthService = outputWidthService;
        _loginGates = loginGates;
        _connectionManager = connectionManager;
        _loginHandler = loginHandler;
        _spawner = spawner;
        _watchRegistry = watchRegistry;
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

        // Color renders first (outermost), then the word-wrapper runs on the rendered
        // output (innermost, just above the raw transport) where ANSI escapes are
        // zero-width -- so wrap width matches exactly what the terminal shows.
        IConnection connection = OutputChainFactory.Build(
            rawConnection, _colorRenderer, _outputWrapper, _outputWidthService, _sessions, _watchRegistry);
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
