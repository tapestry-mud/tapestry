using FluentAssertions;

namespace Tapestry.Engine.Tests;

public class SessionManagerAccountTests
{
    private readonly SessionManager _sessions = new();

    private PlayerSession MakeSession(string name, Guid accountId)
    {
        var conn = new FakeConnection();
        var entity = new Entity("player", name);
        var session = new PlayerSession(conn, entity, accountId);
        session.Phase = LoginPhase.Playing;
        return session;
    }

    [Fact]
    public void GetByAccountId_ReturnsSessionsForAccount()
    {
        var accountId = Guid.NewGuid();
        var session = MakeSession("Mallek", accountId);
        _sessions.Add(session);

        _sessions.GetByAccountId(accountId).Should().ContainSingle()
            .Which.PlayerEntity.Name.Should().Be("Mallek");
    }

    [Fact]
    public void GetByAccountId_UnknownAccount_ReturnsEmpty()
    {
        _sessions.GetByAccountId(Guid.NewGuid()).Should().BeEmpty();
    }

    [Fact]
    public void ActiveCharacterCount_CountsPlayingSessions()
    {
        var accountId = Guid.NewGuid();
        var session = MakeSession("Mallek", accountId);
        _sessions.Add(session);

        _sessions.ActiveCharacterCount(accountId).Should().Be(1);
    }

    [Fact]
    public void ActiveCharacterCount_ExcludesSpecifiedEntity()
    {
        var accountId = Guid.NewGuid();
        var session = MakeSession("Mallek", accountId);
        _sessions.Add(session);

        _sessions.ActiveCharacterCount(accountId, excludeEntityId: session.PlayerEntity.Id)
            .Should().Be(0);
    }

    [Fact]
    public void Remove_CleansUpAccountIndex()
    {
        var accountId = Guid.NewGuid();
        var session = MakeSession("Mallek", accountId);
        _sessions.Add(session);

        _sessions.Remove(session);

        _sessions.GetByAccountId(accountId).Should().BeEmpty();
    }

    [Fact]
    public void AccountId_SetAtConstruction()
    {
        var accountId = Guid.NewGuid();
        var session = MakeSession("Mallek", accountId);
        session.AccountId.Should().Be(accountId);
    }
}
