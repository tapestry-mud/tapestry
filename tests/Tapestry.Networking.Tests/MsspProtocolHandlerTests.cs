using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Tapestry.Networking;
using Tapestry.Shared;

namespace Tapestry.Networking.Tests;

public class MsspProtocolHandlerTests
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
    public async Task NegotiateAsync_sends_IAC_WILL_MSSP()
    {
        var (serverTcp, clientTcp) = CreatePair();
        try
        {
            var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);
            var config = new MsspConfig { Name = "TestMUD", Port = 4000 };
            var handler = new MsspProtocolHandler(config, () => new MsspDynamicValues { Players = 0, UptimeEpoch = 0 });

            await handler.NegotiateAsync(conn, CancellationToken.None);

            var buf = new byte[64];
            var n = await clientTcp.GetStream().ReadAsync(buf);
            var received = buf[..n];

            received.Should().Contain(new byte[] { TelnetProtocolConstants.IAC, TelnetProtocolConstants.WILL, TelnetProtocolConstants.OPT_MSSP });
        }
        finally { serverTcp.Dispose(); clientTcp.Dispose(); }
    }

    [Fact]
    public async Task HandleRemoteDo_sends_variable_table_subneg()
    {
        var (serverTcp, clientTcp) = CreatePair();
        try
        {
            var conn = new TelnetConnection(serverTcp, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelnetConnection>.Instance);
            var config = new MsspConfig { Name = "TestMUD", Port = 4000 };
            var handler = new MsspProtocolHandler(config, () => new MsspDynamicValues { Players = 3, UptimeEpoch = 1000 });

            await handler.NegotiateAsync(conn, CancellationToken.None);
            handler.HandleRemoteDo(conn);

            var buf = new byte[1024];
            var n = await clientTcp.GetStream().ReadAsync(buf);
            var received = buf[..n].ToList();

            // Should start with IAC WILL MSSP, then IAC SB MSSP ... IAC SE
            var sbStart = received.IndexOf(TelnetProtocolConstants.SB);
            sbStart.Should().BeGreaterThan(0);

            // Frame should begin with IAC SB MSSP
            received[sbStart - 1].Should().Be(TelnetProtocolConstants.IAC);
            received[sbStart + 1].Should().Be(TelnetProtocolConstants.OPT_MSSP);

            // Frame should end with IAC SE
            var last = received.ToArray();
            last[^1].Should().Be(TelnetProtocolConstants.SE);
            last[^2].Should().Be(TelnetProtocolConstants.IAC);

            // Frame should contain MSSP_VAR "NAME" bytes
            received.Skip(sbStart + 2).ToArray().Should().Contain(TelnetProtocolConstants.MSSP_VAR);
        }
        finally { serverTcp.Dispose(); clientTcp.Dispose(); }
    }

    [Fact]
    public void IsSessionLong_is_false()
    {
        var handler = new MsspProtocolHandler(
            new MsspConfig { Name = "Test", Port = 4000 },
            () => new MsspDynamicValues());
        handler.IsSessionLong.Should().BeFalse();
    }

    [Fact]
    public void OptionCode_is_70()
    {
        var handler = new MsspProtocolHandler(
            new MsspConfig { Name = "Test", Port = 4000 },
            () => new MsspDynamicValues());
        handler.OptionCode.Should().Be(TelnetProtocolConstants.OPT_MSSP);
    }
}
