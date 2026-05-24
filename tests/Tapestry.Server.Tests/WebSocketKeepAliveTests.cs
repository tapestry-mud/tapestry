using FluentAssertions;

namespace Tapestry.Server.Tests;

public class WebSocketKeepAliveTests
{
    [Fact]
    public void BuildAcceptContext_sets_ping_interval_from_interval_seconds()
    {
        var ctx = WebSocketKeepAlive.BuildAcceptContext(intervalSeconds: 15, retryCount: 4);

        ctx.KeepAliveInterval.Should().Be(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void BuildAcceptContext_sets_abort_timeout_to_interval_times_retries()
    {
        // The abort timeout is what actually defeats the zombie: if no pong arrives within
        // interval * retries, the runtime aborts ReceiveAsync, which fires OnDisconnected.
        var ctx = WebSocketKeepAlive.BuildAcceptContext(intervalSeconds: 15, retryCount: 4);

        ctx.KeepAliveTimeout.Should().Be(TimeSpan.FromSeconds(60));
    }
}
