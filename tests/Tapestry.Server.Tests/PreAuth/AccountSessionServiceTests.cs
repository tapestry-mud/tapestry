using Tapestry.Server.PreAuth;

namespace Tapestry.Server.Tests.PreAuth;

public class AccountSessionServiceTests
{
    [Fact]
    public void Issue_ReturnsNonEmptyToken()
    {
        var svc = new AccountSessionService();
        var token = svc.Issue(Guid.NewGuid());
        Assert.False(string.IsNullOrEmpty(token));
    }

    [Fact]
    public void Consume_ReturnsSameAccountId_WhenTokenValid()
    {
        var svc = new AccountSessionService();
        var accountId = Guid.NewGuid();
        var token = svc.Issue(accountId);
        var session = svc.Consume(token);
        Assert.NotNull(session);
        Assert.Equal(accountId, session!.AccountId);
    }

    [Fact]
    public void Consume_ReturnsNull_WhenTokenConsumedTwice()
    {
        var svc = new AccountSessionService();
        var token = svc.Issue(Guid.NewGuid());
        svc.Consume(token);
        var result = svc.Consume(token);
        Assert.Null(result);
    }

    [Fact]
    public void Consume_ReturnsNull_WhenTokenUnknown()
    {
        var svc = new AccountSessionService();
        var result = svc.Consume("not-a-real-token");
        Assert.Null(result);
    }

    [Fact]
    public void Consume_ReturnsNull_WhenTokenExpired()
    {
        var svc = new AccountSessionService(expirySeconds: 0);
        var token = svc.Issue(Guid.NewGuid());
        Thread.Sleep(50);
        var result = svc.Consume(token);
        Assert.Null(result);
    }
}
