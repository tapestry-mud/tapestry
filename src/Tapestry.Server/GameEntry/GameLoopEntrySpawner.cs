using Tapestry.Engine;
using Tapestry.Engine.Login;
using Tapestry.Engine.Persistence;
using Tapestry.Shared;

namespace Tapestry.Server.GameEntry;

/// <summary>
/// Thread barrier in front of the real spawner. Login resolves on a thread-pool thread
/// (ConnectionHandler and the web pre-auth path both dispatch it with Task.Run), while
/// committing a character into the world mutates the World and publishes engine events
/// that fan out to pack scripts. Script dispatch enters one shared Jint engine that has no
/// synchronization of its own, so a commit racing the loop's own script invocation can tear
/// a call in progress. This decorator posts every commit through GameLoop.Schedule, which
/// drains at the top of Tick -- the same mechanism the loop already documents for work
/// "posted from network threads to run on game loop thread".
///
/// Schedule is a FIFO queue, so a caller that issues several commits in order (the resolver
/// restores world objects immediately before completing the login that owns them) keeps that
/// order on the loop.
/// </summary>
public class GameLoopEntrySpawner : IGameEntrySpawner
{
    private readonly IGameEntrySpawner _inner;
    private readonly GameLoop _gameLoop;

    public GameLoopEntrySpawner(IGameEntrySpawner inner, GameLoop gameLoop)
    {
        _inner = inner;
        _gameLoop = gameLoop;
    }

    public void RestoreWorldObjects(PlayerLoadResult data)
    {
        _gameLoop.Schedule(() => _inner.RestoreWorldObjects(data));
    }

    public void CompleteLogin(Entity entity, IConnection connection, LoginContext preLogin, Guid accountId)
    {
        _gameLoop.Schedule(() => _inner.CompleteLogin(entity, connection, preLogin, accountId));
    }

    public void TakeOverSession(PlayerSession existing, IConnection newConnection, LoginContext preLogin)
    {
        _gameLoop.Schedule(() => _inner.TakeOverSession(existing, newConnection, preLogin));
    }

    public void ReconnectLinkDead(PlayerSession session, IConnection newConnection, LoginContext preLogin)
    {
        _gameLoop.Schedule(() => _inner.ReconnectLinkDead(session, newConnection, preLogin));
    }
}
