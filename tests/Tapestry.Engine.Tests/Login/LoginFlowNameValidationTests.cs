using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Login;
using Tapestry.Server.Login;

namespace Tapestry.Engine.Tests.Login;

public class LoginFlowNameValidationTests
{
    private static (AsyncConnectionAdapter adapter, FakeConnection conn, LoginContext ctx) BuildParts(string id = "t1")
    {
        var conn = new FakeConnection(id);
        var adapter = new AsyncConnectionAdapter(conn);
        var ctx = new LoginContext(id, conn, LoginPhase.Connected);
        return (adapter, conn, ctx);
    }

    private static ServerConfig MakeConfig()
    {
        return new ServerConfig
        {
            Server = new ServerSection { Name = "Test Server" },
            Persistence = new PersistenceSection { PasswordMinLength = 6, MaxLoginAttempts = 3 },
            Idle = new IdleSection { PhaseTimeouts = new PhaseTimeoutsSection { Name = 60 } }
        };
    }

    private static LoginFlow BuildFlow(
        AsyncConnectionAdapter adapter,
        LoginContext ctx,
        SessionManager sessions)
    {
        return new LoginFlow(
            adapter, ctx,
            persistence: null!,
            sessions,
            loginGates: null!,
            gmcp: null,
            config: MakeConfig(),
            logger: NullLogger<LoginFlow>.Instance,
            metrics: null!,
            flowEngine: null);
    }

    [Fact]
    public async Task EmptyName_SendsReprompt()
    {
        var (adapter, conn, ctx) = BuildParts("t1");
        var sessions = new SessionManager();
        sessions.RegisterPreLogin(ctx);
        var flow = BuildFlow(adapter, ctx, sessions);

        var runTask = flow.RunAsync(spawner: null!);

        conn.SimulateInput("");
        conn.Disconnect("test done");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        conn.SentLines.Should().Contain(l => l.Contains("Please enter a name."));
    }

    [Fact]
    public async Task InvalidName_TooShort_SendsError()
    {
        var (adapter, conn, ctx) = BuildParts("t2");
        var sessions = new SessionManager();
        sessions.RegisterPreLogin(ctx);
        var flow = BuildFlow(adapter, ctx, sessions);

        var runTask = flow.RunAsync(spawner: null!);

        conn.SimulateInput("A");
        conn.Disconnect("test done");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        conn.SentLines.Should().Contain(l => l.Contains("Names must be 2-20 letters only."));
    }

    [Fact]
    public async Task InvalidName_WithNumbers_SendsError()
    {
        var (adapter, conn, ctx) = BuildParts("t3");
        var sessions = new SessionManager();
        sessions.RegisterPreLogin(ctx);
        var flow = BuildFlow(adapter, ctx, sessions);

        var runTask = flow.RunAsync(spawner: null!);

        conn.SimulateInput("abc123");
        conn.Disconnect("test done");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        conn.SentLines.Should().Contain(l => l.Contains("Names must be 2-20 letters only."));
    }
}
