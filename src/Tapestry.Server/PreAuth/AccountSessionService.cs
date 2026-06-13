using System.Collections.Concurrent;

namespace Tapestry.Server.PreAuth;

public class AccountSessionService
{
    private const int DefaultExpirySeconds = 120;
    private readonly ConcurrentDictionary<string, AccountSession> _sessions = new();
    private readonly int _expirySeconds;

    public AccountSessionService(int expirySeconds = DefaultExpirySeconds)
    {
        _expirySeconds = expirySeconds;
    }

    public string Issue(Guid accountId)
    {
        var session = new AccountSession(accountId, _expirySeconds);
        _sessions[session.Id] = session;
        return session.Id;
    }

    public AccountSession? Consume(string id)
    {
        if (!_sessions.TryGetValue(id, out var session))
        {
            return null;
        }
        if (!session.IsValid)
        {
            _sessions.TryRemove(id, out _);
            return null;
        }
        session.Used = true;
        _sessions.TryRemove(id, out _);
        return session;
    }
}
