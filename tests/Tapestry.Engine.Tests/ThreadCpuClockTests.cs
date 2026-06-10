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
        // Spin on the CPU clock itself with a generous wall-time escape: a
        // contended shared runner can deschedule this thread for most of a
        // fixed wall window (a 100ms-wall spin measured 38ms CPU on a busy
        // CI runner), but it cannot stop accumulated compute from moving
        // the CPU clock - which is the property under test.
        var start = ThreadCpuClock.GetCurrentThreadCpuTime();
        var sw = Stopwatch.StartNew();
        while ((ThreadCpuClock.GetCurrentThreadCpuTime() - start).TotalMilliseconds < 45
               && sw.Elapsed.TotalMilliseconds < 5000)
        {
            _ = Guid.NewGuid();
        }
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
