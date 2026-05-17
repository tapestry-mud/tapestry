using System.Collections.Concurrent;

namespace Tapestry.Server.PreAuth;

public class PreAuthTokenService
{
    private readonly ConcurrentDictionary<string, PreAuthToken> _tokens = new();
    private readonly int _expirySeconds;

    public PreAuthTokenService(int expirySeconds)
    {
        _expirySeconds = expirySeconds;
    }

    public string Issue(string name, Guid accountId, PreAuthIntent intent)
    {
        var token = new PreAuthToken(name, accountId, intent, _expirySeconds);
        _tokens[token.Id] = token;
        return token.Id;
    }

    public PreAuthToken? Consume(string id)
    {
        if (!_tokens.TryGetValue(id, out var token))
        {
            return null;
        }

        if (!token.IsValid)
        {
            _tokens.TryRemove(id, out _);
            return null;
        }

        token.Used = true;
        _tokens.TryRemove(id, out _);
        return token;
    }
}
