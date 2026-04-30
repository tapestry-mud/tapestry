using Tapestry.Engine;

namespace Tapestry.Engine.Tests;

public class TickTimerTests
{
    [Fact]
    public void SecondsToTicks_ConvertsCorrectly()
    {
        var timer = new TickTimer(10); // 10 ticks/second
        Assert.Equal(20L, timer.SecondsToTicks(2.0));
        Assert.Equal(15L, timer.SecondsToTicks(1.5));
        Assert.Equal(0L, timer.SecondsToTicks(0));
    }

    [Fact]
    public void TicksToSeconds_ConvertsCorrectly()
    {
        var timer = new TickTimer(10);
        Assert.Equal(2.0, timer.TicksToSeconds(20));
        Assert.Equal(0.5, timer.TicksToSeconds(5));
    }

    [Fact]
    public void CurrentTick_StartsAtZero()
    {
        var timer = new TickTimer(10);
        Assert.Equal(0L, timer.CurrentTick);
    }

    [Fact]
    public void Advance_IncrementsCurrentTick()
    {
        var timer = new TickTimer(10);
        timer.Advance();
        timer.Advance();
        timer.Advance();
        Assert.Equal(3L, timer.CurrentTick);
    }

    [Fact]
    public void TicksPerSecond_ReflectsConstructorArg()
    {
        var timer = new TickTimer(20);
        Assert.Equal(20, timer.TicksPerSecond);
        Assert.Equal(40L, timer.SecondsToTicks(2.0));
    }
}
