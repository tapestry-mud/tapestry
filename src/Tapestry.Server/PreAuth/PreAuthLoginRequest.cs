namespace Tapestry.Server.PreAuth;

public record PreAuthLoginRequest(
    string? Name,
    string? Password,
    string? ConfirmPassword);
