using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Networking;

namespace Tapestry.Networking.Tests;

// Byte-level acceptance tests for telnet#90 -- CRLF normalization at TelnetConnection.SendText.
// Primary gate: unit tests here. Telnet scenario runner cannot assert raw bytes.
public class TelnetCrlfNormalizationTests
{
    [Fact]
    public async Task SendText_normalizes_lone_LF_to_CRLF()
    {
        var (server, client) = CreatePair();
        try
        {
            MakeConn(server).SendText("a\nb");
            server.Client.Shutdown(SocketShutdown.Send);

            var received = await ReadAllAsync(client);
            received.Should().Equal(Encoding.UTF8.GetBytes("a\r\nb"));
        }
        finally { server.Dispose(); client.Dispose(); }
    }

    [Fact]
    public async Task SendText_leaves_existing_CRLF_unchanged()
    {
        // Idempotency: an already-correct \r\n must not become \r\r\n.
        var (server, client) = CreatePair();
        try
        {
            MakeConn(server).SendText("a\r\nb");
            server.Client.Shutdown(SocketShutdown.Send);

            var received = await ReadAllAsync(client);
            received.Should().Equal(Encoding.UTF8.GetBytes("a\r\nb"));
        }
        finally { server.Dispose(); client.Dispose(); }
    }

    [Fact]
    public async Task SendText_normalizes_mixed_body_correctly()
    {
        // "a\nb\r\nc\n" -- mix of lone LF and existing CRLF -- must become "a\r\nb\r\nc\r\n".
        var (server, client) = CreatePair();
        try
        {
            MakeConn(server).SendText("a\nb\r\nc\n");
            server.Client.Shutdown(SocketShutdown.Send);

            var received = await ReadAllAsync(client);
            received.Should().Equal(Encoding.UTF8.GetBytes("a\r\nb\r\nc\r\n"));
        }
        finally { server.Dispose(); client.Dispose(); }
    }

    [Fact]
    public async Task SendText_handles_split_chunk_CR_LF_without_doubling()
    {
        // Chunk 1 ends with CR; chunk 2 starts with LF.
        // The LF is already paired with the CR from chunk 1 -- no extra CR must be inserted.
        // A naive implementation that does not carry state across calls would produce \r\r\n here.
        var (server, client) = CreatePair();
        try
        {
            var conn = MakeConn(server);
            conn.SendText("x\r");
            conn.SendText("\ny");
            server.Client.Shutdown(SocketShutdown.Send);

            var received = await ReadAllAsync(client);
            received.Should().Equal(Encoding.UTF8.GetBytes("x\r\ny"));
        }
        finally { server.Dispose(); client.Dispose(); }
    }

    [Fact]
    public async Task SendText_preserves_bare_CR_without_following_LF()
    {
        var (server, client) = CreatePair();
        try
        {
            MakeConn(server).SendText("a\rb");
            server.Client.Shutdown(SocketShutdown.Send);

            var received = await ReadAllAsync(client);
            received.Should().Equal(Encoding.UTF8.GetBytes("a\rb"));
        }
        finally { server.Dispose(); client.Dispose(); }
    }

    [Fact]
    public async Task WebSocketConnection_SendText_does_not_normalize_lone_LF()
    {
        // WebSocketConnection serializes text to JSON; it must NOT apply CRLF normalization.
        // Web clients interpret \n themselves; injecting \r would corrupt web rendering.
        var fakeWs = new FakeWebSocket();
        var conn = new WebSocketConnection(fakeWs, NullLogger<WebSocketConnection>.Instance);

        using var cts = new CancellationTokenSource(2000);
        _ = conn.RunAsync(cts.Token);

        conn.SendText("line1\nline2");

        // Give the async write loop one tick to flush the channel entry.
        await Task.Delay(100);
        await cts.CancelAsync();

        fakeWs.SentFrames.Should().NotBeEmpty();
        var json = Encoding.UTF8.GetString(fakeWs.SentFrames[0]);

        // JSON-encodes the newline as \n (backslash-n), never \r\n (backslash-r-backslash-n).
        json.Should().Contain("line1\\nline2");
        json.Should().NotContain("\\r\\n");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static TelnetConnection MakeConn(TcpClient server)
        => new(server, NullLogger<TelnetConnection>.Instance);

    private static (TcpClient server, TcpClient client) CreatePair()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        var client = new TcpClient();
        client.Connect(System.Net.IPAddress.Loopback, port);
        var server = listener.AcceptTcpClient();
        listener.Stop();
        return (server, client);
    }

    private static async Task<byte[]> ReadAllAsync(TcpClient client)
    {
        using var cts = new CancellationTokenSource(2000);
        using var ms = new MemoryStream();
        var buf = new byte[256];
        int n;
        while ((n = await client.GetStream().ReadAsync(buf, cts.Token)) > 0)
        {
            ms.Write(buf, 0, n);
        }
        return ms.ToArray();
    }

    // Minimal WebSocket stub -- captures SendAsync frames; ReceiveAsync parks until cancelled.
    private sealed class FakeWebSocket : WebSocket
    {
        public List<byte[]> SentFrames { get; } = new();

        public override WebSocketState State => WebSocketState.Open;
        public override string? SubProtocol => null;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;

        public override void Abort() { }

        public override Task CloseAsync(
            WebSocketCloseStatus s, string? d, CancellationToken ct) => Task.CompletedTask;

        public override Task CloseOutputAsync(
            WebSocketCloseStatus s, string? d, CancellationToken ct) => Task.CompletedTask;

        public override void Dispose() { }

        public override async Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return default!;
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer, WebSocketMessageType messageType,
            bool endOfMessage, CancellationToken ct)
        {
            SentFrames.Add(buffer.ToArray());
            return Task.CompletedTask;
        }
    }
}
