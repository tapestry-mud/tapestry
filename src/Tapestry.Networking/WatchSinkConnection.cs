using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Tapestry.Shared;

namespace Tapestry.Networking;

/// <summary>
/// The anonymous web spectator's WebSocket connection (Slice B of watch mode). Mirrors
/// <see cref="WebSocketConnection"/>'s outbound mechanism (an unbounded channel drained by a write
/// loop that sends UTF-8 text frames) but serializes watch-mode typed frames
/// (<c>watch</c>/<c>roster</c>/<c>status</c>, see <see cref="WatchFrames"/>) instead of the stock
/// <c>text</c>/<c>gmcp</c> frames.
///
/// A watch sink has NO player entity, runs no login state machine, and dispatches no game commands.
/// Its only inbound traffic is the tiny watch control protocol (<c>watch &lt;id&gt;</c> /
/// <c>unwatch</c>), surfaced verbatim via <see cref="OnInput"/> from <c>command</c> frames; GMCP and
/// every other frame type are ignored. The watch tee delivers a watched player's color-rendered,
/// unwrapped output here through <see cref="SendText"/>/<see cref="SendLine"/>, which wrap it as
/// <c>watch</c> frames.
/// </summary>
public sealed class WatchSinkConnection : IWatchSink
{
    private readonly WebSocket _socket;
    private readonly ILogger _logger;
    private readonly Channel<string> _outbound = Channel.CreateUnbounded<string>();
    private bool _disconnectFired;

    public string Id { get; } = Guid.NewGuid().ToString();
    public bool IsConnected => _socket.State == WebSocketState.Open;
    public bool SupportsAnsi => true;
    public string? RemoteAddress { get; init; }

    public event Action<string>? OnInput;
    public event Action? OnDisconnected;
    public event Action<string>? OnDisconnectedWithReason;

    public WatchSinkConnection(WebSocket socket, ILogger logger)
    {
        _socket = socket;
        _logger = logger;
    }

    public void SendText(string text)
    {
        if (!IsConnected) { return; }
        _outbound.Writer.TryWrite(WatchFrames.Watch(text));
    }

    public void SendLine(string text)
    {
        SendText(text + "\r\n");
    }

    public void SendRoster(object roster)
    {
        if (!IsConnected) { return; }
        _outbound.Writer.TryWrite(WatchFrames.Roster(roster));
    }

    public void SendStatus(string message)
    {
        if (!IsConnected) { return; }
        _outbound.Writer.TryWrite(WatchFrames.Status(message));
    }

    public void ClearScreen()
    {
        SendText("\x1b[2J\x1b[H");
    }

    public void Heartbeat()
    {
        // No-op: ASP.NET Core's WebSocket ping/pong already detects a dead peer; the abort surfaces
        // as a WebSocketException in ReadLoopAsync -> Disconnect (same rationale as WebSocketConnection).
    }

    public void SuppressEcho()
    {
        // A watcher never enters a password span; nothing to mask.
    }

    public void RestoreEcho()
    {
        // See SuppressEcho.
    }

    public void Disconnect(string reason)
    {
        if (_disconnectFired) { return; }
        _disconnectFired = true;

        _logger.LogInformation("Watch connection {Id} disconnected: {Reason}", Id, reason);
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
            _logger.LogDebug(ex, "Watch write error on {Id}", Id);
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
                var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Disconnect("client closed");
                    return;
                }

                messageBuffer.Write(buffer, 0, result.Count);

                if (messageBuffer.Length > 65536)
                {
                    _logger.LogWarning("Watch message exceeded 64KB, disconnecting {Id}", Id);
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
            _logger.LogDebug("Watch read error on {Id}: {Error}", Id, ex.Message);
            Disconnect("read error: " + ex.Message);
        }
    }

    // The watch control protocol is inbound-minimal: only `command` frames are honored
    // ('watch <id>' / 'unwatch'), surfaced verbatim via OnInput. A watcher has no entity and no
    // command dispatch, so GMCP and any other frame type are ignored.
    private void ProcessMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeElement)) { return; }
            if (typeElement.GetString() != "command") { return; }

            if (root.TryGetProperty("data", out var dataElement))
            {
                OnInput?.Invoke(dataElement.GetString() ?? "");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Invalid JSON from watch {Id}", Id);
        }
    }
}
