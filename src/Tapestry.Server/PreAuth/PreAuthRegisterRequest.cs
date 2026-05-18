namespace Tapestry.Server.PreAuth;

public record PreAuthRegisterRequest(
    string? Email,
    string? Password,
    string? Character);
