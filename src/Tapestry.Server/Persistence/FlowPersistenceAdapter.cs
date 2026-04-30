using Tapestry.Engine;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Persistence;

namespace Tapestry.Server.Persistence;

public class FlowPersistenceAdapter : IFlowPersistence
{
    private readonly PlayerPersistenceService _service;

    public FlowPersistenceAdapter(PlayerPersistenceService service)
    {
        _service = service;
    }

    public bool PlayerExists(string name)
    {
        { return _service.PlayerSaveExists(name); }
    }

    public void SaveNewPlayer(Entity entity, string passwordHash)
    {
        // Called on connection input thread during flow completion.
        // GetAwaiter().GetResult() is safe here — no async context to deadlock against.
        { _service.SaveNewPlayer(entity, passwordHash).GetAwaiter().GetResult(); }
    }
}
