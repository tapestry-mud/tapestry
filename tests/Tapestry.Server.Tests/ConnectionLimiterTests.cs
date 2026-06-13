using Tapestry.Server;

namespace Tapestry.Server.Tests;

public class ConnectionLimiterTests
{
    [Fact]
    public void TryAcquire_ReturnsTrue_WhenUnderCap()
    {
        var limiter = new ConnectionLimiter(3);
        Assert.True(limiter.TryAcquire());
    }

    [Fact]
    public void TryAcquire_ReturnsFalse_WhenAtCap()
    {
        var limiter = new ConnectionLimiter(2);
        limiter.TryAcquire();
        limiter.TryAcquire();
        Assert.False(limiter.TryAcquire());
    }

    [Fact]
    public void TryAcquire_ReturnsTrue_WhenNthConnectionAccepted()
    {
        var limiter = new ConnectionLimiter(3);
        limiter.TryAcquire();
        limiter.TryAcquire();
        Assert.True(limiter.TryAcquire());
    }

    [Fact]
    public void TryAcquire_ReturnsTrue_AfterRelease_WhenWasAtCap()
    {
        var limiter = new ConnectionLimiter(1);
        Assert.True(limiter.TryAcquire());
        Assert.False(limiter.TryAcquire());
        limiter.Release();
        Assert.True(limiter.TryAcquire());
    }

    [Fact]
    public void Current_ReflectsAcquiredCount()
    {
        var limiter = new ConnectionLimiter(10);
        limiter.TryAcquire();
        limiter.TryAcquire();
        limiter.TryAcquire();
        Assert.Equal(3, limiter.Current);
    }

    [Fact]
    public void Release_ReducesCurrentBelowCap()
    {
        var limiter = new ConnectionLimiter(2);
        limiter.TryAcquire();
        limiter.TryAcquire();
        limiter.Release();
        Assert.Equal(1, limiter.Current);
    }
}
