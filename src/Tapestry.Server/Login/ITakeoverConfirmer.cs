namespace Tapestry.Server.Login;

/// <summary>Obtains the player's decision when a takeover of a live session is
/// required. The interactive implementation prompts over the connection;
/// tests use a stub.</summary>
public interface ITakeoverConfirmer
{
    Task<bool> ConfirmAsync(CancellationToken ct);
}
