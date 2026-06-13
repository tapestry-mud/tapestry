using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Tapestry.Shared;

namespace Tapestry.Networking;

public class TelnetServer
{
    private readonly int _port;
    private readonly int _negotiationTimeoutMs;
    private readonly bool _keepAliveEnabled;
    private readonly int _keepAliveIdleSeconds;
    private readonly int _keepAliveIntervalSeconds;
    private readonly int _keepAliveRetryCount;
    private readonly int _keepAliveUserTimeoutMs;
    private readonly MsspConfig? _msspConfig;
    private readonly Func<MsspDynamicValues>? _getMsspDynamic;
    private readonly ILogger<TelnetServer> _logger;
    private TcpListener? _listener;
    private readonly List<TelnetConnection> _connections = new();
    private readonly object _connectionsLock = new();

    public event Action<IConnection>? OnConnectionAccepted;

    /// <summary>Called on each accepted socket. Returns false to refuse over-cap. Null = always accept.</summary>
    public Func<bool>? ConnectionGate { get; set; }

    /// <summary>Called whenever an acquired connection slot is freed (disconnect or early-setup failure).</summary>
    public Action? OnConnectionReleased { get; set; }

    public TelnetServer(
        int port,
        int negotiationTimeoutMs,
        ILogger<TelnetServer> logger,
        bool keepAliveEnabled = true,
        int keepAliveIdleSeconds = 60,
        int keepAliveIntervalSeconds = 15,
        int keepAliveRetryCount = 4,
        int keepAliveUserTimeoutMs = 30000,
        MsspConfig? msspConfig = null,
        Func<MsspDynamicValues>? getMsspDynamic = null)
    {
        _port = port;
        _negotiationTimeoutMs = negotiationTimeoutMs;
        _keepAliveEnabled = keepAliveEnabled;
        _keepAliveIdleSeconds = keepAliveIdleSeconds;
        _keepAliveIntervalSeconds = keepAliveIntervalSeconds;
        _keepAliveRetryCount = keepAliveRetryCount;
        _keepAliveUserTimeoutMs = keepAliveUserTimeoutMs;
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

                if (_keepAliveEnabled)
                {
                    try
                    {
                        TcpKeepAlive.Apply(client.Client, _keepAliveIdleSeconds,
                            _keepAliveIntervalSeconds, _keepAliveRetryCount, _keepAliveUserTimeoutMs);
                    }
                    catch (SocketException ex)
                    {
                        // Never let a keepalive-tuning quirk kill the accept loop.
                        _logger.LogWarning(ex, "Failed to enable TCP keepalive for {Remote}",
                            client.Client.RemoteEndPoint);
                    }
                }

                if (ConnectionGate != null && !ConnectionGate())
                {
                    try
                    {
                        var msg = System.Text.Encoding.ASCII.GetBytes("Server full. Try again later.\r\n");
                        await client.GetStream().WriteAsync(msg, ct).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Best-effort notice; the socket may already be gone.
                    }
                    client.Close();
                    continue;
                }

                TelnetConnection connection;
                try
                {
                    connection = new TelnetConnection(client, _logger);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to initialize connection from {Remote}", client.Client.RemoteEndPoint);
                    OnConnectionReleased?.Invoke();
                    client.Close();
                    continue;
                }
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
                    OnConnectionReleased?.Invoke();
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
