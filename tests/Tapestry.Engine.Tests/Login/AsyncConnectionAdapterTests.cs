using FluentAssertions;
using Tapestry.Engine;

namespace Tapestry.Engine.Tests.Login;

public class AsyncConnectionAdapterTests
{
    [Fact]
    public async Task ReadLineAsync_ReturnsInputWhenFired()
    {
        var conn = new FakeConnection("a1");
        var adapter = new AsyncConnectionAdapter(conn);

        var readTask = adapter.ReadLineAsync(CancellationToken.None);
        conn.SimulateInput("hello");

        var result = await readTask;

        result.Should().Be("hello");
    }

    [Fact]
    public async Task ReadLineAsync_CancelledByToken()
    {
        var conn = new FakeConnection("a2");
        var adapter = new AsyncConnectionAdapter(conn);
        var cts = new CancellationTokenSource();

        var readTask = adapter.ReadLineAsync(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readTask);
    }

    [Fact]
    public async Task ReadLineAsync_CancelledByDisconnect()
    {
        var conn = new FakeConnection("a3");
        var adapter = new AsyncConnectionAdapter(conn);

        var readTask = adapter.ReadLineAsync(CancellationToken.None);
        conn.Disconnect("test");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readTask);
    }

    [Fact]
    public void InputArrivingWithNoPendingRead_IsDropped()
    {
        var conn = new FakeConnection("a4");
        var _ = new AsyncConnectionAdapter(conn);

        // Should not throw
        conn.SimulateInput("stray input");
    }

    [Fact]
    public void SendLine_DelegatesToConnection()
    {
        var conn = new FakeConnection("a5");
        var adapter = new AsyncConnectionAdapter(conn);

        adapter.SendLine("test message");

        conn.SentLines.Should().Contain(l => l.Contains("test message"));
    }

    [Fact]
    public void SuppressEcho_DelegatesToConnection()
    {
        var conn = new FakeConnection("a6");
        var adapter = new AsyncConnectionAdapter(conn);

        adapter.SuppressEcho();

        conn.EchoSuppressed.Should().BeTrue();
    }

    [Fact]
    public void RestoreEcho_DelegatesToConnection()
    {
        var conn = new FakeConnection("a7");
        var adapter = new AsyncConnectionAdapter(conn);
        adapter.SuppressEcho();

        adapter.RestoreEcho();

        conn.EchoSuppressed.Should().BeFalse();
    }
}
