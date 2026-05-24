using Microsoft.AspNetCore.Http;

namespace Tapestry.Server;

/// <summary>
/// Builds the <see cref="WebSocketAcceptContext"/> that turns on WebSocket-level liveness
/// detection. ASP.NET Core sends Ping frames every <c>KeepAliveInterval</c>; if no Pong (or
/// any frame) arrives within <c>KeepAliveTimeout</c>, the runtime aborts the connection,
/// which surfaces as a WebSocketException in the read loop and fires OnDisconnected.
///
/// The default accept context sends pings but leaves the timeout infinite, so a half-open
/// socket is never detected — this builder sets a finite abort window.
/// Detection window ≈ <paramref name="intervalSeconds"/> * <paramref name="retryCount"/>.
/// </summary>
internal static class WebSocketKeepAlive
{
    internal static WebSocketAcceptContext BuildAcceptContext(int intervalSeconds, int retryCount)
    {
        return new WebSocketAcceptContext
        {
            KeepAliveInterval = TimeSpan.FromSeconds(intervalSeconds),
            KeepAliveTimeout = TimeSpan.FromSeconds(intervalSeconds * retryCount),
        };
    }
}
