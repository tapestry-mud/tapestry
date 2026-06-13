namespace Tapestry.Server.PreAuth;

public record PreAuthSelectRequest(
    string? SessionToken,
    string? NewCharacter);
