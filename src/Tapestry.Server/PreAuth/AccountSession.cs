namespace Tapestry.Server.PreAuth;

public class AccountSession
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public Guid AccountId { get; }
    public DateTimeOffset ExpiresAt { get; }
    public bool Used { get; set; }

    public AccountSession(Guid accountId, int expirySeconds)
    {
        AccountId = accountId;
        ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expirySeconds);
    }

    public bool IsValid => !Used && DateTimeOffset.UtcNow < ExpiresAt;
}
