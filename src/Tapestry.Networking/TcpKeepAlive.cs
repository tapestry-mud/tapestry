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
/// </summary>
public static class TcpKeepAlive
{
    public static void Apply(Socket socket, int idleSeconds, int intervalSeconds, int retryCount)
    {
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, idleSeconds);
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, intervalSeconds);
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, retryCount);
    }
}
