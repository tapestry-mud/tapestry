namespace Tapestry.Engine;

public class TickTimer
{
    private long _currentTick;

    public TickTimer(int ticksPerSecond)
    {
        TicksPerSecond = ticksPerSecond;
    }

    public int TicksPerSecond { get; }
    public long CurrentTick => _currentTick;

    public long SecondsToTicks(double seconds) => (long)(seconds * TicksPerSecond);
    public double TicksToSeconds(long ticks) => (double)ticks / TicksPerSecond;
    public void Advance() => _currentTick++;
}
