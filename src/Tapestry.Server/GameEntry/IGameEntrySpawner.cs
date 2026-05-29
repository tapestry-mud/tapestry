using Tapestry.Engine;
using Tapestry.Engine.Login;
using Tapestry.Engine.Persistence;
using Tapestry.Shared;

namespace Tapestry.Server.GameEntry;

/// <summary>
/// The subset of <see cref="PlayerSpawner"/> operations the
/// <see cref="GameEntryResolver"/> orchestrates. Exists as a seam so the
/// resolver can be unit-tested without the full spawner graph.
/// </summary>
public interface IGameEntrySpawner
{
    void RestoreWorldObjects(PlayerLoadResult data);
    void CompleteLogin(Entity entity, IConnection connection, LoginContext preLogin, Guid accountId);
    void TakeOverSession(PlayerSession existing, IConnection newConnection, LoginContext preLogin);
    void ReconnectLinkDead(PlayerSession session, IConnection newConnection, LoginContext preLogin);
}
