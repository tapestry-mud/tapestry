using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Tapestry.Networking;

namespace Tapestry.Networking.Tests;

public class GmcpProtocolHandlerTests
{
    private static (TcpClient server, TcpClient client) CreatePair()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        var server = listener.AcceptTcpClient();
        listener.Stop();
        return (server, client);
    }

    [Fact]
    public async Task NegotiateAsync_sends_IAC_WILL_GMCP()
    {
        var (serverTcp, clientTcp) = CreatePair();
        try
        {
            var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);
            var handler = new GmcpProtocolHandler();

            await handler.NegotiateAsync(conn, CancellationToken.None);

            var buf = new byte[64];
            var n = await clientTcp.GetStream().ReadAsync(buf);
            buf[..n].Should().Contain(new byte[] { TelnetProtocolConstants.IAC, TelnetProtocolConstants.WILL, TelnetProtocolConstants.OPT_GMCP });
        }
        finally { serverTcp.Dispose(); clientTcp.Dispose(); }
    }

    [Fact]
    public void HandleRemoteDo_sets_GmcpActive()
    {
        var handler = new GmcpProtocolHandler();
        handler.GmcpActive.Should().BeFalse();

        var (serverTcp, clientTcp) = CreatePair();
        try
        {
            var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);
            handler.HandleRemoteDo(conn);
            handler.GmcpActive.Should().BeTrue();
        }
        finally { serverTcp.Dispose(); clientTcp.Dispose(); }
    }

    [Fact]
    public void HandleSubnegotiation_parses_CoreSupportsSet()
    {
        var handler = new GmcpProtocolHandler();
        var payload = Encoding.UTF8.GetBytes("Core.Supports.Set [\"Char.Vitals 1\", \"Room.Info 2\"]");

        handler.HandleSubnegotiation(payload);

        handler.SupportsPackage("Char.Vitals").Should().BeTrue();
        handler.SupportsPackage("Room.Info").Should().BeTrue();
        handler.SupportsPackage("Comm.Channel").Should().BeFalse();
    }

    [Fact]
    public void HandleSubnegotiation_parses_CoreSupportsRemove()
    {
        var handler = new GmcpProtocolHandler();
        handler.HandleSubnegotiation(Encoding.UTF8.GetBytes("Core.Supports.Set [\"Char.Vitals 1\"]"));
        handler.HandleSubnegotiation(Encoding.UTF8.GetBytes("Core.Supports.Remove [\"Char.Vitals 1\"]"));

        handler.SupportsPackage("Char.Vitals").Should().BeFalse();
    }

    [Fact]
    public void SupportsPackage_matches_on_prefix()
    {
        var handler = new GmcpProtocolHandler();
        handler.HandleSubnegotiation(Encoding.UTF8.GetBytes("Core.Supports.Set [\"Char 1\"]"));

        handler.SupportsPackage("Char.Vitals").Should().BeTrue();
        handler.SupportsPackage("Char.Status").Should().BeTrue();
        handler.SupportsPackage("Room.Info").Should().BeFalse();
    }

    [Fact]
    public async Task Send_writes_subneg_frame_with_json()
    {
        var (serverTcp, clientTcp) = CreatePair();
        try
        {
            var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);
            var handler = new GmcpProtocolHandler();
            handler.HandleRemoteDo(conn);
            handler.HandleSubnegotiation(Encoding.UTF8.GetBytes("Core.Supports.Set [\"Char.Vitals 1\"]"));
            handler.SetConnection(conn);

            handler.Send("Char.Vitals", new { hp = 100, maxhp = 100 });

            var buf = new byte[1024];
            var n = await clientTcp.GetStream().ReadAsync(buf);
            var frame = buf[..n];

            frame[0].Should().Be(TelnetProtocolConstants.IAC);
            frame[1].Should().Be(TelnetProtocolConstants.SB);
            frame[2].Should().Be(TelnetProtocolConstants.OPT_GMCP);
            frame[^2].Should().Be(TelnetProtocolConstants.IAC);
            frame[^1].Should().Be(TelnetProtocolConstants.SE);

            var content = Encoding.UTF8.GetString(frame, 3, frame.Length - 5);
            content.Should().StartWith("Char.Vitals ");
            content.Should().Contain("\"hp\"");
        }
        finally { serverTcp.Dispose(); clientTcp.Dispose(); }
    }

    [Fact]
    public async Task Send_writes_frame_regardless_of_CoreSupportsSet()
    {
        // SupportsPackage is informational for scripts; Send always delivers when GmcpActive.
        var (serverTcp, clientTcp) = CreatePair();
        try
        {
            var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);
            var handler = new GmcpProtocolHandler();
            handler.HandleRemoteDo(conn);
            handler.SetConnection(conn);
            // No Core.Supports.Set declared — should still send

            handler.Send("Comm.Channel", new { channel = "say", sender = "Alice", text = "hi" });

            var buf = new byte[1024];
            var n = await clientTcp.GetStream().ReadAsync(buf);
            var frame = buf[..n];
            frame[0].Should().Be(TelnetProtocolConstants.IAC);
            frame[1].Should().Be(TelnetProtocolConstants.SB);
            frame[2].Should().Be(TelnetProtocolConstants.OPT_GMCP);
            var content = Encoding.UTF8.GetString(frame, 3, frame.Length - 5);
            content.Should().StartWith("Comm.Channel ");
        }
        finally { serverTcp.Dispose(); clientTcp.Dispose(); }
    }

    [Fact]
    public void HandleSubnegotiation_fires_OnGmcpMessage()
    {
        var handler = new GmcpProtocolHandler();
        string? receivedPackage = null;
        handler.OnGmcpMessage = (pkg, _) => { receivedPackage = pkg; };

        handler.HandleSubnegotiation(Encoding.UTF8.GetBytes("Core.Hello {\"client\":\"Mudlet\"}"));

        receivedPackage.Should().Be("Core.Hello");
    }

    [Fact]
    public void OptionCode_is_201()
    {
        new GmcpProtocolHandler().OptionCode.Should().Be(TelnetProtocolConstants.OPT_GMCP);
    }

    [Fact]
    public void IsSessionLong_is_true()
    {
        new GmcpProtocolHandler().IsSessionLong.Should().BeTrue();
    }
}
