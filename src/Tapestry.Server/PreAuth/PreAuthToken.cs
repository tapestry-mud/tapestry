namespace Tapestry.Server.PreAuth;

public enum PreAuthIntent
{
    Login,
    Create
}

public class PreAuthToken
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string Name { get; }
    public Guid AccountId { get; }
    public PreAuthIntent Intent { get; }
    public DateTimeOffset ExpiresAt { get; }
    public bool Used { get; set; }

    public PreAuthToken(string name, Guid accountId, PreAuthIntent intent, int expirySeconds)
    {
        Name = name;
        AccountId = accountId;
        Intent = intent;
        ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expirySeconds);
    }

    public bool IsValid => !Used && DateTimeOffset.UtcNow < ExpiresAt;
}
