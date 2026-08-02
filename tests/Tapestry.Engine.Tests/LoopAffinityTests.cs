using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Tests.Registration;

namespace Tapestry.Engine.Tests;

/// <summary>
/// Loop affinity is what lets script dispatch tell a safe caller from an unsafe one. Pack
/// script runs in one shared Jint engine with no locking, so it is only safe inside a tick;
/// a publisher on a thread-pool thread (login used to be one) can tear a call in progress.
/// </summary>
public class LoopAffinityTests
{
    private static GameLoop BuildLoop()
    {
        var sessions = new SessionManager();
        return new GameLoop(
            new CommandRouter(new CommandRegistry(), sessions, new World()),
            sessions, new EventBus(), new SystemEventQueue(),
            NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10),
            new NotificationQueue(), TestRegistrationPolicy.Create());
    }

    [Fact]
    public void Work_scheduled_onto_the_loop_reports_itself_as_on_loop()
    {
        var loop = BuildLoop();
        var observed = false;

        loop.Schedule(() => observed = LoopAffinity.OnLoop);
        loop.Tick();

        observed.Should().BeTrue("scheduled actions drain inside Tick, where script dispatch is safe");
    }

    [Fact]
    public void A_caller_outside_the_tick_does_not_report_itself_as_on_loop()
    {
        var loop = BuildLoop();
        loop.Tick();

        LoopAffinity.OnLoop.Should().BeFalse(
            "the flag must not leak past the tick that set it, or a login thread would look safe");
    }

    [Fact]
    public void LoopStarted_stays_false_until_something_ticks()
    {
        // Guards the diagnostic against false positives: unit tests and boot drive engine
        // services with no loop running, and that is not a violation.
        var loop = BuildLoop();
        loop.Tick();

        LoopAffinity.LoopStarted.Should().BeTrue();
    }

    [Fact]
    public void Affinity_is_per_thread_not_global()
    {
        var loop = BuildLoop();
        var seenInsideTick = false;
        bool? seenOnOtherThread = null;

        loop.Schedule(() =>
        {
            seenInsideTick = LoopAffinity.OnLoop;

            // A separate thread running while a tick is in flight is exactly the login case,
            // and it must not inherit the loop's affinity. Use a dedicated thread rather than
            // Task.Run: blocking on a pool task lets the runtime inline it on this very
            // thread, which would read the tick's own flag and prove nothing.
            var probe = new Thread(() => seenOnOtherThread = LoopAffinity.OnLoop);
            probe.Start();
            probe.Join();
        });

        loop.Tick();

        seenInsideTick.Should().BeTrue();
        seenOnOtherThread.Should().BeFalse();
    }
}
