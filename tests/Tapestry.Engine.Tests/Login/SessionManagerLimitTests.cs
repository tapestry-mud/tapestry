using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Tests;

namespace Tapestry.Engine.Tests.Login;

public class SessionManagerLimitTests
{
    private static PlayerSession MakeSession(Guid accountId, string name, string connId)
    {
        var conn = new FakeConnection(connId);
        var entity = new Entity("player", name);
        var session = new PlayerSession(conn, entity, accountId);
        session.Phase = LoginPhase.Playing;
        return session;
    }

    [Fact]
    public void IsAccountAtCharacterLimit_TrueWhenOtherCharacterCountedAtLimitOfOne()
    {
        var sm = new SessionManager();
        var accountId = Guid.NewGuid();
        var online = MakeSession(accountId, "Alpha", "c1");
        sm.Add(online);

        sm.IsAccountAtCharacterLimit(accountId, maxConcurrent: 1).Should().BeTrue();
    }

    [Fact]
    public void IsAccountAtCharacterLimit_FalseWhenTheOnlyOnlineCharacterIsExcluded()
    {
        var sm = new SessionManager();
        var accountId = Guid.NewGuid();
        var online = MakeSession(accountId, "Alpha", "c1");
        sm.Add(online);

        sm.IsAccountAtCharacterLimit(
            accountId, maxConcurrent: 1, excludeEntityId: online.PlayerEntity.Id)
          .Should().BeFalse();
    }

    [Fact]
    public void IsAccountAtCharacterLimit_FalseWhenNoSessions()
    {
        var sm = new SessionManager();
        sm.IsAccountAtCharacterLimit(Guid.NewGuid(), maxConcurrent: 1).Should().BeFalse();
    }
}
