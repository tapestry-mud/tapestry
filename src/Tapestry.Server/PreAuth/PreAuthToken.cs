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
    public PreAuthIntent Intent { get; }
    public string? HashedPassword { get; }
    public DateTimeOffset ExpiresAt { get; }
    public bool Used { get; set; }

    public PreAuthToken(string name, PreAuthIntent intent, int expirySeconds, string? hashedPassword = null)
    {
        Name = name;
        Intent = intent;
        HashedPassword = hashedPassword;
        ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expirySeconds);
    }

    public bool IsValid => !Used && DateTimeOffset.UtcNow < ExpiresAt;
}
