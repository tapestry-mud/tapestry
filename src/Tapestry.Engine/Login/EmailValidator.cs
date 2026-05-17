// src/Tapestry.Engine/Login/EmailValidator.cs
namespace Tapestry.Engine.Login;

public static class EmailValidator
{
    public static (bool Valid, string? Error) Validate(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return (false, "Email is required.");
        }

        var trimmed = email.Trim();
        var atIndex = trimmed.IndexOf('@');
        if (atIndex < 1 || atIndex != trimmed.LastIndexOf('@'))
        {
            return (false, "Invalid email format.");
        }

        var domain = trimmed[(atIndex + 1)..];
        if (domain.Length == 0 || !domain.Contains('.'))
        {
            return (false, "Invalid email format.");
        }

        return (true, null);
    }

    public static string Normalize(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
