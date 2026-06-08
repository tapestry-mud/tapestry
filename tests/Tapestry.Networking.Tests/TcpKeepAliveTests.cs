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

    // TCP_USER_TIMEOUT is Linux-only and set via a raw socket option; there is no portable
    // getter to assert the value, and the call is a no-op off Linux. We just prove Apply with a
    // user-timeout does not throw on the current platform (so the dev box build/test is safe;
    // the actual TCP_USER_TIMEOUT effect is verified live on the Linux droplet).
    [Fact]
    public void Apply_with_user_timeout_does_not_throw()
    {
        using var socket = CreateSocket();

        var act = () => TcpKeepAlive.Apply(
            socket, idleSeconds: 30, intervalSeconds: 10, retryCount: 3, userTimeoutMs: 30000);

        act.Should().NotThrow();
    }

    [Fact]
    public void Apply_sets_send_timeout_cross_platform()
    {
        using var socket = CreateSocket();

        TcpKeepAlive.Apply(socket, idleSeconds: 30, intervalSeconds: 10, retryCount: 3, userTimeoutMs: 30000);

        socket.SendTimeout.Should().Be(30000);
    }
}
