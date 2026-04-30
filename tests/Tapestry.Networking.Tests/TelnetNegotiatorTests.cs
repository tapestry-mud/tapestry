using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Tapestry.Networking;

namespace Tapestry.Networking.Tests;

public class TelnetNegotiatorTests
{
    private const byte IAC = 255;
    private const byte SB = 250;
    private const byte SE = 240;
    private const byte WILL = 251;
    private const byte WONT = 252;
    private const byte DO = 253;
    private const byte DONT = 254;
    private const byte OPT_TTYPE = 24;
    private const byte OPT_NAWS = 31;

    private static (TcpClient serverTcp, TcpClient clientTcp) CreateStreamPair()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var clientTcp = new TcpClient();
        clientTcp.Connect(IPAddress.Loopback, port);
        var serverTcp = listener.AcceptTcpClient();
        listener.Stop();
        return (serverTcp, clientTcp);
    }

    [Fact]
    public async Task Timeout_returns_default_capabilities()
    {
        var (serverTcp, clientTcp) = CreateStreamPair();
        try
        {
            var negotiator = new TelnetNegotiator(timeoutMs: 200);
            var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);
            var result = await negotiator.NegotiateAsync(conn, CancellationToken.None);

            result.SupportsTtype.Should().BeFalse();
            result.SupportsNaws.Should().BeFalse();
            result.ClientName.Should().BeNull();
            result.UseServerEcho.Should().BeTrue();
            result.ColorSupport.Should().Be(ColorSupport.None);
        }
        finally
        {
            serverTcp.Dispose();
            clientTcp.Dispose();
        }
    }

    [Fact]
    public async Task Negotiates_ttype_from_mud_client()
    {
        var (serverTcp, clientTcp) = CreateStreamPair();
        try
        {
            var negotiator = new TelnetNegotiator(timeoutMs: 2000);
            var clientStream = clientTcp.GetStream();
            var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);

            var negotiateTask = negotiator.NegotiateAsync(conn, CancellationToken.None);

            // Simulate client side
            var buf = new byte[64];
            await clientStream.ReadAsync(buf);

            // Respond WILL TTYPE
            await clientStream.WriteAsync(new byte[] { IAC, WILL, OPT_TTYPE });
            await clientStream.FlushAsync();

            // Read SB TTYPE SEND from server
            await clientStream.ReadAsync(buf);

            // Respond SB TTYPE IS "zmud"
            var ttypeName = Encoding.ASCII.GetBytes("zmud");
            var sbResponse = new byte[] { IAC, SB, OPT_TTYPE, 0 };
            var sbEnd = new byte[] { IAC, SE };
            var fullResponse = sbResponse.Concat(ttypeName).Concat(sbEnd).ToArray();
            await clientStream.WriteAsync(fullResponse);
            await clientStream.FlushAsync();

            // Respond WILL NAWS
            await clientStream.WriteAsync(new byte[] { IAC, WILL, OPT_NAWS });
            await clientStream.FlushAsync();

            // Respond SB NAWS 100x50 (big-endian)
            await clientStream.WriteAsync(new byte[] { IAC, SB, OPT_NAWS, 0, 100, 0, 50, IAC, SE });
            await clientStream.FlushAsync();

            var result = await negotiateTask;

            result.ClientName.Should().Be("zmud");
            result.SupportsTtype.Should().BeTrue();
            result.SupportsNaws.Should().BeTrue();
            result.WindowWidth.Should().Be(100);
            result.WindowHeight.Should().Be(50);
            result.IsMudClient.Should().BeTrue();
            result.UseServerEcho.Should().BeFalse();
            result.ColorSupport.Should().Be(ColorSupport.Extended);
        }
        finally
        {
            serverTcp.Dispose();
            clientTcp.Dispose();
        }
    }

    [Fact]
    public async Task Negotiates_terminal_type()
    {
        var (serverTcp, clientTcp) = CreateStreamPair();
        try
        {
            var negotiator = new TelnetNegotiator(timeoutMs: 2000);
            var clientStream = clientTcp.GetStream();
            var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);

            var negotiateTask = negotiator.NegotiateAsync(conn, CancellationToken.None);

            var buf = new byte[64];
            await clientStream.ReadAsync(buf);

            // Respond WILL TTYPE
            await clientStream.WriteAsync(new byte[] { IAC, WILL, OPT_TTYPE });
            await clientStream.FlushAsync();

            // Read SB TTYPE SEND
            await clientStream.ReadAsync(buf);

            // Respond SB TTYPE IS "ANSI"
            var ttypeName = Encoding.ASCII.GetBytes("ANSI");
            var fullResponse = new byte[] { IAC, SB, OPT_TTYPE, 0 }
                .Concat(ttypeName)
                .Concat(new byte[] { IAC, SE })
                .ToArray();
            await clientStream.WriteAsync(fullResponse);
            await clientStream.FlushAsync();

            // Respond WILL NAWS + SB NAWS 116x60
            await clientStream.WriteAsync(new byte[] { IAC, WILL, OPT_NAWS });
            await clientStream.FlushAsync();

            await clientStream.WriteAsync(new byte[] { IAC, SB, OPT_NAWS, 0, 116, 0, 60, IAC, SE });
            await clientStream.FlushAsync();

            var result = await negotiateTask;

            result.ClientName.Should().Be("ANSI");
            result.IsMudClient.Should().BeFalse();
            result.UseServerEcho.Should().BeTrue();
            result.WindowWidth.Should().Be(116);
            result.WindowHeight.Should().Be(60);
            result.ColorSupport.Should().Be(ColorSupport.Basic);
        }
        finally
        {
            serverTcp.Dispose();
            clientTcp.Dispose();
        }
    }

    [Fact]
    public async Task Naws_only_no_ttype()
    {
        var (serverTcp, clientTcp) = CreateStreamPair();
        try
        {
            var negotiator = new TelnetNegotiator(timeoutMs: 300);
            var clientStream = clientTcp.GetStream();
            var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);

            var negotiateTask = negotiator.NegotiateAsync(conn, CancellationToken.None);

            var buf = new byte[64];
            await clientStream.ReadAsync(buf);

            // Respond WILL NAWS only (ignore TTYPE)
            await clientStream.WriteAsync(new byte[] { IAC, WILL, OPT_NAWS });
            await clientStream.FlushAsync();

            // Respond SB NAWS 80x24
            await clientStream.WriteAsync(new byte[] { IAC, SB, OPT_NAWS, 0, 80, 0, 24, IAC, SE });
            await clientStream.FlushAsync();

            var result = await negotiateTask;

            result.SupportsTtype.Should().BeFalse();
            result.SupportsNaws.Should().BeTrue();
            result.UseServerEcho.Should().BeTrue();
        }
        finally
        {
            serverTcp.Dispose();
            clientTcp.Dispose();
        }
    }

    [Fact]
    public async Task Sends_do_ttype_and_do_naws()
    {
        var (serverTcp, clientTcp) = CreateStreamPair();
        try
        {
            var negotiator = new TelnetNegotiator(timeoutMs: 200);
            var clientStream = clientTcp.GetStream();
            var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);

            var negotiateTask = negotiator.NegotiateAsync(conn, CancellationToken.None);

            var buf = new byte[64];
            var bytesRead = await clientStream.ReadAsync(buf);

            var received = buf[..bytesRead];

            // Should contain IAC DO TTYPE
            received.Should().Contain(new byte[] { IAC, DO, OPT_TTYPE });
            // Should contain IAC DO NAWS
            received.Should().Contain(new byte[] { IAC, DO, OPT_NAWS });

            await negotiateTask;
        }
        finally
        {
            serverTcp.Dispose();
            clientTcp.Dispose();
        }
    }

    [Fact]
    public async Task Negotiator_calls_NegotiateAsync_on_each_handler()
    {
        var (serverTcp, clientTcp) = CreateStreamPair();
        try
        {
            var handler = new TrackingHandler(201);
            var negotiator = new TelnetNegotiator(timeoutMs: 300, handlers: new[] { handler });
            var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);

            var negotiateTask = negotiator.NegotiateAsync(conn, CancellationToken.None);
            // drain client side and let timeout fire
            var buf = new byte[256];
            await clientTcp.GetStream().ReadAsync(buf);
            await negotiateTask;

            handler.NegotiateAsyncCalled.Should().BeTrue();
        }
        finally
        {
            serverTcp.Dispose();
            clientTcp.Dispose();
        }
    }

    [Fact]
    public async Task Negotiator_calls_HandleRemoteDo_when_DO_received_for_handler_option()
    {
        var (serverTcp, clientTcp) = CreateStreamPair();
        try
        {
            var handler = new TrackingHandler(201);
            var negotiator = new TelnetNegotiator(timeoutMs: 2000, handlers: new[] { handler });
            var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);

            var negotiateTask = negotiator.NegotiateAsync(conn, CancellationToken.None);
            var clientStream = clientTcp.GetStream();

            var buf = new byte[256];
            await clientStream.ReadAsync(buf); // drain server opening

            // Client sends DO 201 (GMCP)
            await clientStream.WriteAsync(new byte[] { IAC, DO, 201 });
            // Also satisfy TTYPE and NAWS to let negotiation complete
            await clientStream.WriteAsync(new byte[] { IAC, WILL, OPT_TTYPE });
            await clientStream.ReadAsync(buf); // drain SB TTYPE SEND
            var ttypeName = Encoding.ASCII.GetBytes("mudlet");
            await clientStream.WriteAsync(new byte[] { IAC, SB, OPT_TTYPE, 0 }.Concat(ttypeName).Concat(new byte[] { IAC, SE }).ToArray());
            await clientStream.WriteAsync(new byte[] { IAC, SB, OPT_NAWS, 0, 80, 0, 24, IAC, SE });

            var caps = await negotiateTask;

            handler.HandleRemoteDoCalled.Should().BeTrue();
            caps.SupportsGmcp.Should().BeTrue();
        }
        finally
        {
            serverTcp.Dispose();
            clientTcp.Dispose();
        }
    }

    private class TrackingHandler : IProtocolHandler
    {
        public byte OptionCode { get; }
        public bool IsSessionLong => true;
        public bool NegotiateAsyncCalled { get; private set; }
        public bool HandleRemoteDoCalled { get; private set; }

        public TrackingHandler(byte code) { OptionCode = code; }

        public Task NegotiateAsync(TelnetConnection connection, CancellationToken ct)
        {
            NegotiateAsyncCalled = true;
            return Task.CompletedTask;
        }

        public void HandleRemoteDo(TelnetConnection connection) { HandleRemoteDoCalled = true; }
        public void HandleSubnegotiation(byte[] data) { }
    }
}
