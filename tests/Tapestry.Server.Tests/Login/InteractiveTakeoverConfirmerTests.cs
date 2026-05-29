using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Login;
using Tapestry.Server.Login;
using Tapestry.Server.Tests.Fakes;

namespace Tapestry.Server.Tests.Login;

public class InteractiveTakeoverConfirmerTests
{
    private static (FakeConnection conn, AsyncConnectionAdapter adapter, LoginContext ctx)
        Wire()
    {
        var conn = new FakeConnection("c1");
        var adapter = new AsyncConnectionAdapter(conn);
        var ctx = new LoginContext("c1", conn);
        return (conn, adapter, ctx);
    }

    [Fact]
    public async Task SetsSessionTakeoverPhase_AndPromptsOnConfirm()
    {
        var (conn, adapter, ctx) = Wire();
        var confirmer = new InteractiveTakeoverConfirmer(
            adapter, ctx, loginHandler: null, phaseTimeoutSeconds: 30);

        var task = confirmer.ConfirmAsync(CancellationToken.None);
        conn.SimulateInput("y");
        var result = await task;

        result.Should().BeTrue();
        ctx.Phase.Should().Be(LoginPhase.SessionTakeover);
        conn.SentLines.Should().Contain(l => l.Contains("already connected"));
    }

    [Theory]
    [InlineData("y", true)]
    [InlineData("yes", true)]
    [InlineData("YES", true)]
    [InlineData("n", false)]
    [InlineData("no", false)]
    [InlineData("", false)]
    public async Task ParsesAffirmativeOnly(string input, bool expected)
    {
        var (conn, adapter, ctx) = Wire();
        var confirmer = new InteractiveTakeoverConfirmer(adapter, ctx, null, 30);

        var task = confirmer.ConfirmAsync(CancellationToken.None);
        conn.SimulateInput(input);
        var result = await task;

        result.Should().Be(expected);
    }

    [Fact]
    public async Task ReturnsFalseWhenCancelled()
    {
        var (conn, adapter, ctx) = Wire();
        var confirmer = new InteractiveTakeoverConfirmer(adapter, ctx, null, 30);
        using var cts = new CancellationTokenSource();

        var task = confirmer.ConfirmAsync(cts.Token);
        cts.Cancel();
        var result = await task;

        result.Should().BeFalse();
    }
}
