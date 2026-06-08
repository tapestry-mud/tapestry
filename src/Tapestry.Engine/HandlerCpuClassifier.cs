namespace Tapestry.Engine;

/// <summary>
/// Classifies a slow handler by comparing wall-clock time to thread CPU time.
/// wall ~ cpu  => the handler genuinely did the work (cpu-bound).
/// wall >> cpu  => the handler was preempted/blocked, not actually busy.
/// </summary>
public static class HandlerCpuClassifier
{
    /// <summary>CPU/wall ratio at or above which a handler is considered cpu-bound.</summary>
    public const double CpuBoundRatio = 0.7;

    public static string Classify(double wallMs, double cpuMs, bool cpuSupported)
    {
        if (!cpuSupported) { return "cpu-unknown"; }
        if (wallMs <= 0) { return "cpu-bound"; }
        return cpuMs >= wallMs * CpuBoundRatio ? "cpu-bound" : "preempted";
    }
}
