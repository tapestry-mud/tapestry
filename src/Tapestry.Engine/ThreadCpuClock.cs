using System.Runtime.InteropServices;

namespace Tapestry.Engine;

/// <summary>
/// Reads the CPU time consumed by the *current OS thread*. Used by the game loop
/// to separate genuine handler CPU cost from scheduling preemption: the tick runs
/// synchronously on one thread, so a delta sampled around a handler call is that
/// handler's CPU time (valid even though the loop thread identity may vary per tick).
/// </summary>
public static class ThreadCpuClock
{
    private const int ClockThreadCpuTimeId = 3; // CLOCK_THREAD_CPUTIME_ID (Linux)

    /// <summary>True when a per-thread CPU clock is available for this OS.</summary>
    public static bool IsSupported => OperatingSystem.IsLinux() || OperatingSystem.IsWindows();

    /// <summary>
    /// Total CPU time (user + kernel) consumed by the calling thread so far.
    /// Returns <see cref="TimeSpan.Zero"/> on platforms without an implementation.
    /// </summary>
    public static TimeSpan GetCurrentThreadCpuTime()
    {
        // Telemetry must never crash the game loop. Any native failure -- a failed syscall,
        // or libc/kernel32 not resolving on an unexpected base image -- degrades to Zero.
        // A Zero reading makes a slow handler classify as "preempted" rather than
        // "cpu-bound": a safe, conservative default for a missing measurement.
        try
        {
            if (OperatingSystem.IsLinux())
            {
                var ts = default(Timespec);
                if (clock_gettime(ClockThreadCpuTimeId, ref ts) != 0)
                {
                    return TimeSpan.Zero;
                }
                long nanos = (ts.tv_sec * 1_000_000_000L) + ts.tv_nsec;
                return new TimeSpan(nanos / 100); // TimeSpan ticks are 100ns
            }

            if (OperatingSystem.IsWindows())
            {
                if (!GetThreadTimes(GetCurrentThread(), out _, out _, out long kernel, out long user))
                {
                    return TimeSpan.Zero;
                }
                // kernel/user are FILETIME values already in 100ns units.
                return new TimeSpan(kernel + user);
            }

            return TimeSpan.Zero;
        }
        catch (DllNotFoundException)
        {
            return TimeSpan.Zero;
        }
        catch (EntryPointNotFoundException)
        {
            return TimeSpan.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec
    {
        public long tv_sec;
        public long tv_nsec;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int clock_gettime(int clockid, ref Timespec tp);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetThreadTimes(
        IntPtr hThread, out long creationTime, out long exitTime, out long kernelTime, out long userTime);
}
