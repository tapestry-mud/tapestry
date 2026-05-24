using System.Net.Sockets;
using FluentAssertions;
using Tapestry.Networking;

namespace Tapestry.Networking.Tests;

public class TcpKeepAliveTests
{
    private static Socket CreateSocket() =>
        new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    [Fact]
    public void Apply_enables_so_keepalive()
    {
        using var socket = CreateSocket();

        TcpKeepAlive.Apply(socket, idleSeconds: 60, intervalSeconds: 15, retryCount: 4);

        var enabled = (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)!;
        enabled.Should().NotBe(0);
    }

    [Fact]
    public void Apply_sets_tcp_keepalive_tuning_from_arguments()
    {
        using var socket = CreateSocket();

        TcpKeepAlive.Apply(socket, idleSeconds: 30, intervalSeconds: 10, retryCount: 3);

        ((int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)!)
            .Should().Be(30);
        ((int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval)!)
            .Should().Be(10);
        ((int)socket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount)!)
            .Should().Be(3);
    }
}
