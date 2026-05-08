namespace Tapestry.Data;

public class PreAuthSection
{
    public bool Enabled { get; set; } = false;
    public int TokenExpirySeconds { get; set; } = 60;
}
