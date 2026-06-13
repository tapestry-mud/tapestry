namespace Tapestry.Server.Login;

public static class PasswordValidator
{
    public static (bool ok, string? error) Validate(string password, int minLength)
    {
        if (password.Length < minLength)
        {
            return (false, $"Password must be at least {minLength} characters.");
        }
        return (true, null);
    }
}
