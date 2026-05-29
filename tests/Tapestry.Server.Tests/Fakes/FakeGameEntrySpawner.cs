using Tapestry.Engine;
using Tapestry.Engine.Login;
using Tapestry.Engine.Persistence;
using Tapestry.Server.GameEntry;
using Tapestry.Shared;

namespace Tapestry.Server.Tests.Fakes;

public class FakeGameEntrySpawner : IGameEntrySpawner
{
    public bool RestoreCalled { get; private set; }
    public bool CompleteLoginCalled { get; private set; }
    public bool TakeOverCalled { get; private set; }
    public bool ReconnectCalled { get; private set; }

    public void RestoreWorldObjects(PlayerLoadResult data) { RestoreCalled = true; }

    public void CompleteLogin(Entity entity, IConnection connection, LoginContext preLogin, Guid accountId)
    {
        CompleteLoginCalled = true;
    }

    public void TakeOverSession(PlayerSession existing, IConnection newConnection, LoginContext preLogin)
    {
        TakeOverCalled = true;
    }

    public void ReconnectLinkDead(PlayerSession session, IConnection newConnection, LoginContext preLogin)
    {
        ReconnectCalled = true;
    }
}
