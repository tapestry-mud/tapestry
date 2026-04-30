using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Tapestry.Shared;

namespace Tapestry.Networking;

public class TelnetServer
{
    private readonly int _port;
    private readonly int _negotiationTimeoutMs;
    private readonly MsspConfig? _msspConfig;
    private readonly Func<MsspDynamicValues>? _getMsspDynamic;
    private readonly ILogger<TelnetServer> _logger;
    private TcpListener? _listener;
    private readonly List<TelnetConnection> _connections = new();
    private readonly object _connectionsLock = new();

    public event Action<IConnection>? OnConnectionAccepted;

    public TelnetServer(
        int port,
        int negotiationTimeoutMs,
        ILogger<TelnetServer> logger,
        MsspConfig? msspConfig = null,
        Func<MsspDynamicValues>? getMsspDynamic = null)
    {
        _port = port;
        _negotiationTimeoutMs = negotiationTimeoutMs;
        _logger = logger;
        _msspConfig = msspConfig;
        _getMsspDynamic = getMsspDynamic;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _logger.LogInformation("Telnet server listening on port {Port}", _port);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                var connection = new TelnetConnection(client, _logger);
                _logger.LogInformation("New telnet connection: {Id} from {Remote}", connection.Id, client.Client.RemoteEndPoint);

                try
                {
                    var handlers = BuildHandlers();
                    var negotiator = new TelnetNegotiator(_negotiationTimeoutMs, handlers);
                    var caps = await negotiator.NegotiateAsync(connection, ct);
                    connection.SetCapabilities(caps);
                    _logger.LogInformation(
                        "Connection {Id} negotiated: TTYPE={Ttype}, NAWS={Width}x{Height}, Echo={Echo}, GMCP={Gmcp}",
                        connection.Id,
                        caps.ClientName ?? "(none)",
                        caps.WindowWidth,
                        caps.WindowHeight,
                        caps.UseServerEcho ? "server" : "client",
                        caps.SupportsGmcp);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Negotiation failed for {Id}, using defaults", connection.Id);
                    connection.SetCapabilities(ClientCapabilities.Default);
                }

                lock (_connectionsLock)
                {
                    _connections.Add(connection);
                }

                connection.OnDisconnected += () =>
                {
                    lock (_connectionsLock)
                    {
                        _connections.Remove(connection);
                    }
                };

                OnConnectionAccepted?.Invoke(connection);

                var readTask = connection.ReadLoopAsync(ct);
#pragma warning disable CS4014
                readTask.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        _logger.LogError(t.Exception, "Unhandled error in ReadLoopAsync for connection {Id}", connection.Id);
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore CS4014
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            _listener.Stop();

            List<TelnetConnection> toClose;
            lock (_connectionsLock)
            {
                toClose = new List<TelnetConnection>(_connections);
                _connections.Clear();
            }
            foreach (var conn in toClose)
            {
                conn.Disconnect("server shutdown");
            }

            _logger.LogInformation("Telnet server stopped");
        }
    }

    private List<IProtocolHandler> BuildHandlers()
    {
        var handlers = new List<IProtocolHandler>();

        if (_msspConfig != null && _getMsspDynamic != null)
        {
            handlers.Add(new MsspProtocolHandler(_msspConfig, _getMsspDynamic));
        }

        handlers.Add(new GmcpProtocolHandler());

        return handlers;
    }
}
