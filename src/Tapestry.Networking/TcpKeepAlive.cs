using System.Net.Sockets;

namespace Tapestry.Networking;

/// <summary>
/// Enables OS-level TCP keepalive on a socket so the kernel actively probes the peer.
/// Without this, a half-open connection (client vanished without a clean FIN) leaves the
/// telnet read loop blocked forever — the session never fires OnDisconnected and the player
/// zombies in the world. With keepalive, a failed probe sequence surfaces as a read error,
/// which tears the connection down through the normal disconnect path.
///
/// Detection window ≈ <paramref name="idleSeconds"/> + <paramref name="intervalSeconds"/> *
/// <paramref name="retryCount"/>.
///
/// On Linux we additionally set TCP_USER_TIMEOUT, which bounds how long an *unacknowledged*
/// write may stay outstanding before the kernel errors the socket. This is the backstop the
/// heartbeat relies on: some kernels (notably the prod droplet's) ignore the per-socket
/// keepalive idle/interval knobs, so a write to a vanished peer would otherwise hang ~15min.
/// </summary>
public static class TcpKeepAlive
{
    // From <netinet/tcp.h>: IPPROTO_TCP level, TCP_USER_TIMEOUT option (value in milliseconds).
    private const int IpprotoTcp = 6;
    private const int TcpUserTimeout = 18;

    public static void Apply(Socket socket, int idleSeconds, int intervalSeconds, int retryCount,
        int userTimeoutMs = 0)
    {
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, idleSeconds);
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, intervalSeconds);
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, retryCount);

        if (userTimeoutMs > 0 && OperatingSystem.IsLinux())
        {
            socket.SetRawSocketOption(IpprotoTcp, TcpUserTimeout, BitConverter.GetBytes(userTimeoutMs));
        }
    }
}
