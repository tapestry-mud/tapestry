using System.Diagnostics;
using FluentAssertions;
using Tapestry.Engine;

namespace Tapestry.Engine.Tests;

public class ThreadCpuClockTests
{
    [Fact]
    public void IsSupported_OnDevAndCi_IsTrue()
    {
        // Dev is Windows, CI is Linux -- both implemented.
        ThreadCpuClock.IsSupported.Should().BeTrue();
    }

    [Fact]
    public void GetCurrentThreadCpuTime_BusySpin_ConsumesCpuTime()
    {
        var start = ThreadCpuClock.GetCurrentThreadCpuTime();
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalMilliseconds < 100) { _ = Guid.NewGuid(); }
        var cpu = (ThreadCpuClock.GetCurrentThreadCpuTime() - start).TotalMilliseconds;

        cpu.Should().BeGreaterThan(40, "a busy spin consumes substantial CPU");
    }

    [Fact]
    public void GetCurrentThreadCpuTime_Sleep_ConsumesAlmostNoCpuTime()
    {
        var start = ThreadCpuClock.GetCurrentThreadCpuTime();
        Thread.Sleep(120);
        var cpu = (ThreadCpuClock.GetCurrentThreadCpuTime() - start).TotalMilliseconds;

        // A sleeping thread burns ~0 CPU even though 120ms of wall time elapsed.
        // This is the decisive proof the clock measures CPU, not wall.
        cpu.Should().BeLessThan(30);
    }

    [Fact]
    public void GetCurrentThreadCpuTime_IsMonotonicNonDecreasing()
    {
        var a = ThreadCpuClock.GetCurrentThreadCpuTime();
        var b = ThreadCpuClock.GetCurrentThreadCpuTime();
        (b - a).Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }
}
