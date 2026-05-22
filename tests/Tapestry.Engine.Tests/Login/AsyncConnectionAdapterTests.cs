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
    public async Task InputArrivingWithNoPendingRead_IsBufferedUntilNextRead()
    {
        var conn = new FakeConnection("a4");
        var adapter = new AsyncConnectionAdapter(conn);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        conn.SimulateInput("typed ahead"); // arrives before any ReadLineAsync

        var result = await adapter.ReadLineAsync(cts.Token);

        result.Should().Be("typed ahead");
    }

    [Fact]
    public async Task BufferedInput_IsReturnedInFifoOrder()
    {
        var conn = new FakeConnection("a8");
        var adapter = new AsyncConnectionAdapter(conn);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        conn.SimulateInput("first");
        conn.SimulateInput("second");

        (await adapter.ReadLineAsync(cts.Token)).Should().Be("first");
        (await adapter.ReadLineAsync(cts.Token)).Should().Be("second");
    }

    [Fact]
    public async Task BufferedInput_IsCappedToBound()
    {
        var conn = new FakeConnection("a9");
        var adapter = new AsyncConnectionAdapter(conn);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        for (var i = 0; i < AsyncConnectionAdapter.MaxBufferedInputLines + 10; i++)
        {
            conn.SimulateInput($"line{i}");
        }

        // The first MaxBufferedInputLines are retained in order; overflow is dropped.
        for (var i = 0; i < AsyncConnectionAdapter.MaxBufferedInputLines; i++)
        {
            (await adapter.ReadLineAsync(cts.Token)).Should().Be($"line{i}");
        }

        // Nothing buffered beyond the cap: the next read stays pending.
        var pending = adapter.ReadLineAsync(CancellationToken.None);
        pending.IsCompleted.Should().BeFalse();
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
