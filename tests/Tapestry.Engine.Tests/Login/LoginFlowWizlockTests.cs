using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Login;
using Tapestry.Engine.Persistence;
using Tapestry.Server.Login;

namespace Tapestry.Engine.Tests.Login;

/// <summary>
/// Wizlock enforcement on the existing-character login path: the persisted
/// save (and therefore its roles) is loaded before the password prompt, so a
/// locked game refuses non-admins outright while admins proceed normally.
/// </summary>
public class LoginFlowWizlockTests
{
    private class FakePlayerStore : IPlayerStore
    {
        private readonly Dictionary<string, PlayerSaveData> _data = new(StringComparer.OrdinalIgnoreCase);

        public void Seed(PlayerSaveData data)
        {
            _data[data.Name] = data;
        }

        public Task SaveAsync(PlayerSaveData data)
        {
            _data[data.Name] = data;
            return Task.CompletedTask;
        }

        public Task<PlayerSaveData?> LoadAsync(string playerName)
        {
            _data.TryGetValue(playerName, out var d);
            return Task.FromResult(d);
        }

        public bool Exists(string playerName)
        {
            return _data.ContainsKey(playerName);
        }

        public Task DeleteAsync(string playerName)
        {
            _data.Remove(playerName);
            return Task.CompletedTask;
        }

        public IReadOnlyList<string> GetSupplementalFileTypes(string playerName)
        {
            return Array.Empty<string>();
        }
    }

    private record Harness(LoginFlow Flow, FakeConnection Conn, WizlockState Wizlock);

    private static Harness Build(string name, bool admin, bool locked)
    {
        var registry = new PropertyRegistry();
        CommonProperties.Register(registry);
        var serializer = new PlayerSerializer(registry);

        var entity = new Entity("player", name);
        if (admin)
        {
            entity.AddRole("admin");
        }

        var store = new FakePlayerStore();
        store.Seed(serializer.ToSaveData(entity, Guid.NewGuid(), new List<Entity>()));

        var sessions = new SessionManager();
        var world = new World();
        var persistence = new PlayerPersistenceService(
            store, serializer, sessions, world,
            NullLogger<PlayerPersistenceService>.Instance);

        var conn = new FakeConnection();
        var adapter = new AsyncConnectionAdapter(conn);
        var ctx = new LoginContext(conn.Id, conn, LoginPhase.Connected);
        sessions.RegisterPreLogin(ctx);

        var config = new ServerConfig
        {
            Server = new ServerSection { Name = "Test Server" },
            Persistence = new PersistenceSection { PasswordMinLength = 6, MaxLoginAttempts = 3 },
            Idle = new IdleSection { PhaseTimeouts = new PhaseTimeoutsSection { Name = 60, Password = 60 } }
        };

        var wizlock = new WizlockState { Locked = locked };

        var flow = new LoginFlow(
            adapter, ctx,
            persistence,
            accountService: null!,
            sessions,
            loginGates: null!,
            loginHandler: null,
            config,
            NullLogger<LoginFlow>.Instance,
            metrics: null!,
            wizlock,
            flowEngine: null);

        return new Harness(flow, conn, wizlock);
    }

    [Fact]
    public async Task Locked_NonAdminLogin_IsRefusedAndDisconnected()
    {
        var h = Build("Alice", admin: false, locked: true);

        var runTask = h.Flow.RunAsync(spawner: null!);
        h.Conn.SimulateInput("Alice");
        await runTask;

        h.Conn.SentLines.Should().Contain(l => l.Contains("The game is wizlocked."));
        h.Conn.SentLines.Should().NotContain(l => l.Contains("Password:"));
        h.Conn.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task Locked_AdminLogin_ProceedsToPasswordPrompt()
    {
        var h = Build("Boss", admin: true, locked: true);

        var runTask = h.Flow.RunAsync(spawner: null!);
        h.Conn.SimulateInput("Boss");
        h.Conn.Disconnect("test done");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        h.Conn.SentLines.Should().Contain(l => l.Contains("Password:"));
        h.Conn.SentLines.Should().NotContain(l => l.Contains("The game is wizlocked."));
    }

    [Fact]
    public async Task Unlocked_NonAdminLogin_ProceedsToPasswordPrompt()
    {
        var h = Build("Alice", admin: false, locked: false);

        var runTask = h.Flow.RunAsync(spawner: null!);
        h.Conn.SimulateInput("Alice");
        h.Conn.Disconnect("test done");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        h.Conn.SentLines.Should().Contain(l => l.Contains("Password:"));
        h.Conn.SentLines.Should().NotContain(l => l.Contains("The game is wizlocked."));
    }
}
