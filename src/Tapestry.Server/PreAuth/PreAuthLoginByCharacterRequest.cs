namespace Tapestry.Server.PreAuth;

public record PreAuthLoginByCharacterRequest(
    string? Character,
    string? Password);
