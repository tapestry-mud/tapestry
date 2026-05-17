// src/Tapestry.Engine/Persistence/AccountSaveData.cs
namespace Tapestry.Engine.Persistence;

public class AccountSaveData
{
    public Guid Id { get; set; } = Guid.Empty;
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public List<string> Characters { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool EmailVerified { get; set; } = false;
    public DateTime? VerifiedAt { get; set; } = null;
}
