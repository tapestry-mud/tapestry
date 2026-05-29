using FluentAssertions;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Login;
using Tapestry.Engine.Persistence;
using Tapestry.Server.GameEntry;
using Tapestry.Server.Tests.Fakes;

namespace Tapestry.Server.Tests.GameEntry;

public class GameEntryResolverTests
{
    private static ServerConfig ConfigWithLimit(int limit)
    {
        var config = new ServerConfig();
        config.Accounts.MaxConcurrentCharacters = limit;
        return config;
    }

    private static PlayerLoadResult LoadedData(Guid accountId, string name, out Entity entity)
    {
        entity = new Entity("player", name);
        return new PlayerLoadResult
        {
            Entity = entity,
            AccountId = accountId,
            AllItems = new List<Entity>(),
        };
    }

    private static PlayerSession ExistingSession(
        SessionManager sm, Guid accountId, string name, LoginPhase phase)
    {
        var conn = new FakeConnection();
        var entity = new Entity("player", name);
        var session = new PlayerSession(conn, entity, accountId) { Phase = phase };
        sm.Add(session);
        if (phase == LoginPhase.LinkDead)
        {
            sm.RemoveConnectionOnly(session); // mirror real link-dead bookkeeping
        }
        return session;
    }

    [Fact]
    public async Task LinkDeadSession_Reconnects()
    {
        var sm = new SessionManager();
        var accountId = Guid.NewGuid();
        ExistingSession(sm, accountId, "Alpha", LoginPhase.LinkDead);
        var spawner = new FakeGameEntrySpawner();
        var resolver = new GameEntryResolver(sm, spawner, ConfigWithLimit(1));
        var conn = new FakeConnection();
        var ctx = new LoginContext(conn.Id, conn);
        var data = LoadedData(accountId, "Alpha", out _);

        var result = await resolver.ResolveAsync(
            accountId, "Alpha", data, conn, ctx,
            new StubTakeoverConfirmer(false), CancellationToken.None);

        result.Should().Be(GameEntryResult.Reconnected);
        spawner.ReconnectCalled.Should().BeTrue();
        spawner.CompleteLoginCalled.Should().BeFalse();
    }

    [Fact]
    public async Task PlayingSession_ConfirmYes_TakesOver()
    {
        var sm = new SessionManager();
        var accountId = Guid.NewGuid();
        ExistingSession(sm, accountId, "Alpha", LoginPhase.Playing);
        var spawner = new FakeGameEntrySpawner();
        var resolver = new GameEntryResolver(sm, spawner, ConfigWithLimit(1));
        var conn = new FakeConnection();
        var ctx = new LoginContext(conn.Id, conn);
        var data = LoadedData(accountId, "Alpha", out _);

        var result = await resolver.ResolveAsync(
            accountId, "Alpha", data, conn, ctx,
            new StubTakeoverConfirmer(true), CancellationToken.None);

        result.Should().Be(GameEntryResult.TookOver);
        spawner.TakeOverCalled.Should().BeTrue();
    }

    [Fact]
    public async Task PlayingSession_ConfirmNo_Declines()
    {
        var sm = new SessionManager();
        var accountId = Guid.NewGuid();
        ExistingSession(sm, accountId, "Alpha", LoginPhase.Playing);
        var spawner = new FakeGameEntrySpawner();
        var resolver = new GameEntryResolver(sm, spawner, ConfigWithLimit(1));
        var conn = new FakeConnection();
        var ctx = new LoginContext(conn.Id, conn);
        var data = LoadedData(accountId, "Alpha", out _);

        var result = await resolver.ResolveAsync(
            accountId, "Alpha", data, conn, ctx,
            new StubTakeoverConfirmer(false), CancellationToken.None);

        result.Should().Be(GameEntryResult.Declined);
        spawner.TakeOverCalled.Should().BeFalse();
        spawner.CompleteLoginCalled.Should().BeFalse();
    }

    [Fact]
    public async Task NoSession_UnderLimit_Spawns()
    {
        var sm = new SessionManager();
        var accountId = Guid.NewGuid();
        var spawner = new FakeGameEntrySpawner();
        var resolver = new GameEntryResolver(sm, spawner, ConfigWithLimit(1));
        var conn = new FakeConnection();
        var ctx = new LoginContext(conn.Id, conn);
        var data = LoadedData(accountId, "Alpha", out _);

        var result = await resolver.ResolveAsync(
            accountId, "Alpha", data, conn, ctx,
            new StubTakeoverConfirmer(false), CancellationToken.None);

        result.Should().Be(GameEntryResult.Spawned);
        spawner.RestoreCalled.Should().BeTrue();
        spawner.CompleteLoginCalled.Should().BeTrue();
    }

    [Fact]
    public async Task NoSession_AtLimitWithDifferentCharacter_OverLimit()
    {
        var sm = new SessionManager();
        var accountId = Guid.NewGuid();
        ExistingSession(sm, accountId, "Bravo", LoginPhase.Playing);
        var spawner = new FakeGameEntrySpawner();
        var resolver = new GameEntryResolver(sm, spawner, ConfigWithLimit(1));
        var conn = new FakeConnection();
        var ctx = new LoginContext(conn.Id, conn);
        var data = LoadedData(accountId, "Alpha", out _);

        var result = await resolver.ResolveAsync(
            accountId, "Alpha", data, conn, ctx,
            new StubTakeoverConfirmer(false), CancellationToken.None);

        result.Should().Be(GameEntryResult.OverLimit);
        spawner.CompleteLoginCalled.Should().BeFalse();
    }

    [Fact]
    public async Task AtLimitButOnlyOnlineCharacterIsTarget_NotOverLimit()
    {
        var sm = new SessionManager();
        var accountId = Guid.NewGuid();
        ExistingSession(sm, accountId, "Alpha", LoginPhase.Playing);
        var spawner = new FakeGameEntrySpawner();
        var resolver = new GameEntryResolver(sm, spawner, ConfigWithLimit(1));
        var conn = new FakeConnection();
        var ctx = new LoginContext(conn.Id, conn);
        var data = LoadedData(accountId, "Alpha", out _);

        var result = await resolver.ResolveAsync(
            accountId, "Alpha", data, conn, ctx,
            new StubTakeoverConfirmer(true), CancellationToken.None);

        result.Should().Be(GameEntryResult.TookOver);
        result.Should().NotBe(GameEntryResult.OverLimit);
    }
}
