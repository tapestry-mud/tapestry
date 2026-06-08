using FluentAssertions;
using Tapestry.Engine;

namespace Tapestry.Engine.Tests;

public class HandlerCpuClassifierTests
{
    [Fact]
    public void Classify_HighWallLowCpu_IsPreempted()
        => HandlerCpuClassifier.Classify(wallMs: 80, cpuMs: 2, cpuSupported: true)
            .Should().Be("preempted");

    [Fact]
    public void Classify_WallApproxCpu_IsCpuBound()
        => HandlerCpuClassifier.Classify(wallMs: 78, cpuMs: 77, cpuSupported: true)
            .Should().Be("cpu-bound");

    [Fact]
    public void Classify_AtRatioBoundary_IsCpuBound()
        // cpu == wall * 0.7 exactly counts as cpu-bound (inclusive).
        => HandlerCpuClassifier.Classify(wallMs: 50, cpuMs: 35, cpuSupported: true)
            .Should().Be("cpu-bound");

    [Fact]
    public void Classify_JustBelowRatioBoundary_IsPreempted()
        => HandlerCpuClassifier.Classify(wallMs: 50, cpuMs: 34.9, cpuSupported: true)
            .Should().Be("preempted");

    [Fact]
    public void Classify_WhenCpuUnsupported_IsCpuUnknown()
        => HandlerCpuClassifier.Classify(wallMs: 80, cpuMs: 0, cpuSupported: false)
            .Should().Be("cpu-unknown");
}
