using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Tapestry.Shared;

namespace Tapestry.Networking;

public class WebSocketConnection : IConnection
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly WebSocket _socket;
    private readonly ILogger _logger;
    private readonly Channel<string> _outbound = Channel.CreateUnbounded<string>();
    private readonly WebSocketGmcpHandler _gmcpHandler;
    private bool _disconnectFired;

    public string Id { get; } = Guid.NewGuid().ToString();
    public bool IsConnected => _socket.State == WebSocketState.Open;
    public bool SupportsAnsi => true;
    public WebSocketGmcpHandler GmcpHandler => _gmcpHandler;

    public event Action<string>? OnInput;
    public event Action? OnDisconnected;
    public event Action<string>? OnDisconnectedWithReason;

    public WebSocketConnection(WebSocket socket, ILogger logger)
    {
        _socket = socket;
        _logger = logger;
        _gmcpHandler = new WebSocketGmcpHandler(EnqueueGmcp);
    }

    public void SendText(string text)
    {
        if (!IsConnected) { return; }
        var json = JsonSerializer.Serialize(new { type = "text", data = text });
        _outbound.Writer.TryWrite(json);
    }

    public void SendLine(string text)
    {
        SendText(text + "\r\n");
    }

    public void ClearScreen()
    {
        SendText("\x1b[2J\x1b[H");
    }

    public void SuppressEcho()
    {
        // Web client handles password masking locally
    }

    public void RestoreEcho()
    {
        // Web client handles password masking locally
    }

    public void Disconnect(string reason)
    {
        if (_disconnectFired) { return; }
        _disconnectFired = true;

        _logger.LogInformation("WebSocket connection {Id} disconnected: {Reason}", Id, reason);
        _outbound.Writer.TryComplete();

        try
        {
            if (_socket.State == WebSocketState.Open ||
                _socket.State == WebSocketState.CloseReceived)
            {
                _socket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        }
        catch
        {
            // Ignore close errors
        }

        OnDisconnectedWithReason?.Invoke(reason);
        OnDisconnected?.Invoke();
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var readTask = ReadLoopAsync(ct);
        var writeTask = WriteLoopAsync(ct);

        try
        {
            await Task.WhenAny(readTask, writeTask);
        }
        finally
        {
            _outbound.Writer.TryComplete();
            if (!_disconnectFired)
            {
                Disconnect("connection ended");
            }
        }
    }

    private async Task WriteLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var message in _outbound.Reader.ReadAllAsync(ct))
            {
                if (_socket.State != WebSocketState.Open) { break; }
                var bytes = Encoding.UTF8.GetBytes(message);
                await _socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "WebSocket write error on {Id}", Id);
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        using var messageBuffer = new MemoryStream();

        try
        {
            while (_socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await _socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Disconnect("client closed");
                    return;
                }

                messageBuffer.Write(buffer, 0, result.Count);

                if (messageBuffer.Length > 65536)
                {
                    _logger.LogWarning(
                        "WebSocket message exceeded 64KB, disconnecting {Id}", Id);
                    Disconnect("message too large");
                    return;
                }

                if (result.EndOfMessage)
                {
                    var json = Encoding.UTF8.GetString(
                        messageBuffer.GetBuffer(), 0, (int)messageBuffer.Length);
                    messageBuffer.SetLength(0);
                    ProcessMessage(json);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Disconnect("server shutdown");
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug("WebSocket read error on {Id}: {Error}", Id, ex.Message);
            Disconnect("read error: " + ex.Message);
        }
    }

    private void ProcessMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeElement)) { return; }
            var type = typeElement.GetString();

            switch (type)
            {
                case "command":
                    if (root.TryGetProperty("data", out var dataElement))
                    {
                        var command = dataElement.GetString();
                        if (!string.IsNullOrEmpty(command))
                        {
                            OnInput?.Invoke(command);
                        }
                    }
                    break;

                case "gmcp":
                    if (root.TryGetProperty("package", out var pkgElement) &&
                        root.TryGetProperty("data", out var gmcpData))
                    {
                        var package = pkgElement.GetString();
                        if (package != null)
                        {
                            _gmcpHandler.HandleIncoming(package, gmcpData.Clone());
                        }
                    }
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Invalid JSON from WebSocket {Id}", Id);
        }
    }

    private void EnqueueGmcp(string package, object payload)
    {
        if (!IsConnected) { return; }
        var envelope = new { type = "gmcp", package, data = payload };
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        _outbound.Writer.TryWrite(json);
    }
}
