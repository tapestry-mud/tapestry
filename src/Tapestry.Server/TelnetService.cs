using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tapestry.Data;
using Tapestry.Networking;
using Tapestry.Shared;

namespace Tapestry.Server;

public class TelnetService : IHostedService
{
    private readonly TelnetServer _telnet;
    private readonly ConnectionHandler _connectionHandler;
    private readonly ServerConfig _config;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<TelnetService> _logger;
    private Task? _runTask;

    public TelnetService(
        TelnetServer telnet,
        ConnectionHandler connectionHandler,
        ServerConfig config,
        IHostApplicationLifetime appLifetime,
        ILogger<TelnetService> logger)
    {
        _telnet = telnet;
        _connectionHandler = connectionHandler;
        _config = config;
        _appLifetime = appLifetime;
        _logger = logger;

        _telnet.OnConnectionAccepted += HandleNewConnection;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Telnet server starting on port {Port}", _config.Server.TelnetPort);
        _runTask = _telnet.StartAsync(_appLifetime.ApplicationStopping);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Telnet server stopping.");
        if (_runTask != null)
        {
            try
            {
                await _runTask;
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }
    }

    private void HandleNewConnection(IConnection rawConnection)
    {
        IGmcpHandler? gmcpHandler = null;
        if (rawConnection is TelnetConnection telnetConn)
        {
            var handler = telnetConn.Router?.GetHandler<GmcpProtocolHandler>(
                TelnetProtocolConstants.OPT_GMCP);
            if (handler != null)
            {
                gmcpHandler = handler;
            }
        }

        _connectionHandler.HandleNewConnection(rawConnection, gmcpHandler);
    }
}
