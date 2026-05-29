using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Login;
using Tapestry.Engine.Persistence;
using Tapestry.Server.Login;
using Tapestry.Shared;

namespace Tapestry.Server.GameEntry;

/// <summary>
/// The single authority that decides how an already-authenticated identity
/// enters the game: reconnect to a link-dead session, take over a live one,
/// reject for the concurrent-character limit, or spawn fresh. Shared by telnet,
/// direct-WS, and the web pre-auth path.
/// </summary>
public class GameEntryResolver
{
    private readonly SessionManager _sessions;
    private readonly IGameEntrySpawner _spawner;
    private readonly ServerConfig _config;

    public GameEntryResolver(SessionManager sessions, IGameEntrySpawner spawner, ServerConfig config)
    {
        _sessions = sessions;
        _spawner = spawner;
        _config = config;
    }

    public async Task<GameEntryResult> ResolveAsync(
        Guid accountId,
        string characterName,
        PlayerLoadResult loadedData,
        IConnection connection,
        LoginContext loginContext,
        ITakeoverConfirmer confirmer,
        CancellationToken ct)
    {
        var existing = _sessions.GetByPlayerName(characterName);
        if (existing != null)
        {
            if (existing.Phase == LoginPhase.LinkDead)
            {
                _spawner.ReconnectLinkDead(existing, connection, loginContext);
                return GameEntryResult.Reconnected;
            }

            // Playing (live, or an as-yet-undetected zombie) -> confirm takeover.
            if (await confirmer.ConfirmAsync(ct))
            {
                _spawner.TakeOverSession(existing, connection, loginContext);
                return GameEntryResult.TookOver;
            }

            return GameEntryResult.Declined;
        }

        // No existing session for this character -> fresh spawn, subject to the
        // limit. Exclude the target entity id; reconnect/takeover never reach here.
        if (_sessions.IsAccountAtCharacterLimit(
                accountId, _config.Accounts.MaxConcurrentCharacters, loadedData.Entity.Id))
        {
            return GameEntryResult.OverLimit;
        }

        _spawner.RestoreWorldObjects(loadedData);
        _spawner.CompleteLogin(loadedData.Entity, connection, loginContext, accountId);
        return GameEntryResult.Spawned;
    }
}
