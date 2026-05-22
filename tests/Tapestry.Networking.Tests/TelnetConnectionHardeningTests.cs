using FluentAssertions;
using Tapestry.Networking;

namespace Tapestry.Networking.Tests;

public class TelnetConnectionHardeningTests
{
    [Fact]
    public void Constants_are_defined_for_buffer_limits()
    {
        TelnetConnection.MaxLineLength.Should().Be(4096);
        TelnetConnection.MaxBufferSize.Should().Be(65536);
    }

    [Fact]
    public async Task SendSubnegotiation_writes_correct_frame()
    {
        var (serverTcp, clientTcp) = CreatePair();
        try
        {
            var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);

            conn.SendSubnegotiation(201, new byte[] { 0x43, 0x6F, 0x72, 0x65 }); // "Core"

            var buf = new byte[64];
            var n = await clientTcp.GetStream().ReadAsync(buf);
            var received = buf[..n];

            // Expected: IAC SB 201 'C' 'o' 'r' 'e' IAC SE
            received.Should().Equal(255, 250, 201, 0x43, 0x6F, 0x72, 0x65, 255, 240);
        }
        finally
        {
            serverTcp.Dispose();
            clientTcp.Dispose();
        }
    }

    // Integration: proves ReadLoopAsync feeds socket bytes through the parser to
    // the router. Waits for the routed result instead of racing a fixed timer, so
    // it cannot flake on TCP delivery timing.
    [Fact]
    public async Task ReadLoop_feeds_socket_bytes_through_parser_to_router()
    {
        var (serverTcp, clientTcp) = CreatePair();
        try
        {
            using var cts = new CancellationTokenSource(2000);
            var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);

            var router = new TelnetProtocolRouter();
            var handler = new RecordingHandler(201);
            router.Register(handler);
            conn.AttachRouter(router);

            var readTask = conn.ReadLoopAsync(cts.Token);

            // IAC SB 201 0x41 0x42 IAC SE
            await clientTcp.GetStream().WriteAsync(new byte[] { 255, 250, 201, 0x41, 0x42, 255, 240 });

            await WaitUntilAsync(() => handler.ReceivedData is not null, cts.Token);

            handler.ReceivedData.Should().Equal(new byte[] { 0x41, 0x42 });

            cts.Cancel();
            try { await readTask; } catch (OperationCanceledException) { }
        }
        finally
        {
            serverTcp.Dispose();
            clientTcp.Dispose();
        }
    }

    [Fact]
    public void ProcessInboundBytes_routes_complete_subneg()
    {
        var (conn, handler, server, client) = BuildConnection();
        try
        {
            conn.ProcessInboundBytes(new byte[] { 255, 250, 201, 0x41, 0x42, 255, 240 });

            handler.ReceivedData.Should().Equal(new byte[] { 0x41, 0x42 });
        }
        finally { server.Dispose(); client.Dispose(); }
    }

    [Fact]
    public void ProcessInboundBytes_routes_subneg_split_in_data()
    {
        var (conn, handler, server, client) = BuildConnection();
        try
        {
            // "IAC SB 201 0x41" | "0x42 IAC SE"
            conn.ProcessInboundBytes(new byte[] { 255, 250, 201, 0x41 });
            handler.ReceivedData.Should().BeNull(); // subneg not yet terminated

            conn.ProcessInboundBytes(new byte[] { 0x42, 255, 240 });
            handler.ReceivedData.Should().Equal(new byte[] { 0x41, 0x42 });
        }
        finally { server.Dispose(); client.Dispose(); }
    }

    [Fact]
    public void ProcessInboundBytes_routes_subneg_split_at_iac_se()
    {
        var (conn, handler, server, client) = BuildConnection();
        try
        {
            // Split exactly between the terminating IAC and SE: "...0x42 IAC" | "SE"
            conn.ProcessInboundBytes(new byte[] { 255, 250, 201, 0x41, 0x42, 255 });
            handler.ReceivedData.Should().BeNull();

            conn.ProcessInboundBytes(new byte[] { 240 });
            handler.ReceivedData.Should().Equal(new byte[] { 0x41, 0x42 });
        }
        finally { server.Dispose(); client.Dispose(); }
    }

    [Fact]
    public void ProcessInboundBytes_routes_subneg_split_at_iac_sb_option()
    {
        var (conn, handler, server, client) = BuildConnection();
        try
        {
            // Split inside the opening sequence: "IAC SB" | "201 0x41 0x42 IAC SE"
            conn.ProcessInboundBytes(new byte[] { 255, 250 });
            conn.ProcessInboundBytes(new byte[] { 201, 0x41, 0x42, 255, 240 });

            handler.ReceivedData.Should().Equal(new byte[] { 0x41, 0x42 });
        }
        finally { server.Dispose(); client.Dispose(); }
    }

    [Fact]
    public void ProcessInboundBytes_unescapes_doubled_iac_in_subneg_data()
    {
        var (conn, handler, server, client) = BuildConnection();
        try
        {
            // IAC SB 201 0x41 IAC IAC 0x42 IAC SE -> data is {0x41, 0xFF, 0x42}
            conn.ProcessInboundBytes(new byte[] { 255, 250, 201, 0x41, 255, 255, 0x42, 255, 240 });

            handler.ReceivedData.Should().Equal(new byte[] { 0x41, 255, 0x42 });
        }
        finally { server.Dispose(); client.Dispose(); }
    }

    private static (TelnetConnection conn, RecordingHandler handler,
        System.Net.Sockets.TcpClient server, System.Net.Sockets.TcpClient client) BuildConnection(byte option = 201)
    {
        var (serverTcp, clientTcp) = CreatePair();
        var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);
        var handler = new RecordingHandler(option);
        var router = new TelnetProtocolRouter();
        router.Register(handler);
        conn.AttachRouter(router);
        return (conn, handler, serverTcp, clientTcp);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct)
    {
        while (!condition())
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(10, ct);
        }
    }

    private static (System.Net.Sockets.TcpClient server, System.Net.Sockets.TcpClient client) CreatePair()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        var client = new System.Net.Sockets.TcpClient();
        client.Connect(System.Net.IPAddress.Loopback, port);
        var server = listener.AcceptTcpClient();
        listener.Stop();
        return (server, client);
    }

    private class RecordingHandler : IProtocolHandler
    {
        public byte OptionCode { get; }
        public bool IsSessionLong => true;
        public byte[]? ReceivedData { get; private set; }
        public RecordingHandler(byte code) { OptionCode = code; }
        public Task NegotiateAsync(TelnetConnection c, CancellationToken ct) => Task.CompletedTask;
        public void HandleRemoteDo(TelnetConnection c) { }
        public void HandleSubnegotiation(byte[] data) { ReceivedData = data; }
    }
}
