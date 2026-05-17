namespace Tapestry.Server.PreAuth;

public record PreAuthSelectRequest(
    string? AccountId,
    string? Character,
    string? NewCharacter);
