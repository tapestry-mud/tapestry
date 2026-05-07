using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Login;

namespace Tapestry.Engine.Tests.Login;

public class LoginContextTests
{
    [Fact]
    public void LoginContext_InitializesWithConnectedPhase()
    {
        var conn = new FakeConnection("conn-1");
        var ctx = new LoginContext("conn-1", conn);

        ctx.ConnectionId.Should().Be("conn-1");
        ctx.Connection.Should().BeSameAs(conn);
        ctx.Phase.Should().Be(LoginPhase.Connected);
        ctx.ConnectedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void LoginContext_PhaseIsMutable()
    {
        var conn = new FakeConnection("conn-2");
        var ctx = new LoginContext("conn-2", conn);

        ctx.Phase = LoginPhase.Name;

        ctx.Phase.Should().Be(LoginPhase.Name);
    }

    [Fact]
    public void LoginContext_PhaseCtsIsReplaceable()
    {
        var conn = new FakeConnection("conn-3");
        var ctx = new LoginContext("conn-3", conn);
        var oldCts = ctx.PhaseCts;

        ctx.PhaseCts = new CancellationTokenSource();

        ctx.PhaseCts.Should().NotBeSameAs(oldCts);
    }
}
