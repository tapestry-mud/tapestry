namespace Tapestry.Server.PreAuth;

public record PreAuthLoginRequest(
    string? Email,
    string? Password);
