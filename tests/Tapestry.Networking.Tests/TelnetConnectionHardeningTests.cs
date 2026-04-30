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

    [Fact]
    public async Task ReadLoop_routes_subneg_to_router_when_present()
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

            // Send: IAC SB 201 0x41 0x42 IAC SE then a newline to trigger a clean exit
            var frame = new byte[] { 255, 250, 201, 0x41, 0x42, 255, 240 };
            await clientTcp.GetStream().WriteAsync(frame);
            await clientTcp.GetStream().WriteAsync(new byte[] { (byte)'q', (byte)'\n' });

            await Task.Delay(100);
            cts.Cancel();
            try { await readTask; } catch (OperationCanceledException) { }

            handler.ReceivedData.Should().Equal(new byte[] { 0x41, 0x42 });
        }
        finally
        {
            serverTcp.Dispose();
            clientTcp.Dispose();
        }
    }

    [Fact]
    public async Task ReadLoop_routes_cross_buffer_subneg_to_router()
    {
        var (serverTcp, clientTcp) = CreatePair();
        try
        {
            using var cts = new CancellationTokenSource(2000);
            var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);

            var handler = new RecordingHandler(201);
            var router = new TelnetProtocolRouter();
            router.Register(handler);
            conn.AttachRouter(router);

            var readTask = conn.ReadLoopAsync(cts.Token);

            var stream = clientTcp.GetStream();
            // Send IAC SB 201 in one write, data + IAC SE in a second write
            await stream.WriteAsync(new byte[] { 255, 250, 201, 0x41 });
            await Task.Delay(20);
            await stream.WriteAsync(new byte[] { 0x42, 255, 240 });
            await stream.WriteAsync(new byte[] { (byte)'q', (byte)'\n' });

            await Task.Delay(100);
            cts.Cancel();
            try { await readTask; } catch (OperationCanceledException) { }

            handler.ReceivedData.Should().Equal(new byte[] { 0x41, 0x42 });
        }
        finally
        {
            serverTcp.Dispose();
            clientTcp.Dispose();
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
