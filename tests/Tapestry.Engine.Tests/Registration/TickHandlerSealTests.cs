using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Registration;
using Xunit;

namespace Tapestry.Engine.Tests.Registration;

[Collection("GameLoopSerial")]
public class TickHandlerSealTests
{
    private sealed class NoEdges : IPackEdgeOracle { public bool DeclaresEdge(string a, string b) => false; }

    private static (GameLoop loop, RegistrationPolicy policy) CreateLoop()
    {
        var policy = new RegistrationPolicy(new NoEdges());
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var router = new CommandRouter(registry, sessions, world);
        var loop = new GameLoop(router, sessions, new EventBus(), new SystemEventQueue(),
            NullLogger<GameLoop>.Instance, new TapestryMetrics(), new TickTimer(10), new NotificationQueue(), policy);
        return (loop, policy);
    }

    [Fact]
    public void DuplicateName_WithoutOverride_ThrowsAtSeal_NotAtRegister()
    {
        var (loop, policy) = CreateLoop();
        loop.RegisterTickHandler("dup", 1, () => { }, packName: "pack-a"); // both accepted
        loop.RegisterTickHandler("dup", 1, () => { }, packName: "pack-b");
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }

    [Fact]
    public void KernelName_DuplicateRegistration_ThrowsAtSeal()
    {
        var (loop, policy) = CreateLoop();
        loop.RegisterTickHandler("heartbeat", 1, () => { });                 // packName defaults to "kernel"
        loop.RegisterTickHandler("heartbeat", 1, () => { }, packName: "kernel");
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }

    [Fact]
    public void SingleHandler_FiresAfterSeal()
    {
        var (loop, policy) = CreateLoop();
        var fired = 0;
        loop.RegisterTickHandler("beat", 1, () => fired++);
        policy.Resolve();
        loop.Tick();
        fired.Should().Be(1);
    }
}
