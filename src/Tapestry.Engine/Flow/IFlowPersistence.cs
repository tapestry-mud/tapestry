namespace Tapestry.Engine.Flow;

public interface IFlowPersistence
{
    bool PlayerExists(string name);
    void SaveNewPlayer(Entity entity, string passwordHash);
}

/// <summary>
/// No-op implementation registered by default so DI can resolve FlowEngine
/// in test/embedded contexts that don't provide a real persistence layer.
/// The server overrides this with a concrete implementation.
/// </summary>
internal sealed class NullFlowPersistence : IFlowPersistence
{
    public bool PlayerExists(string name) => false;
    public void SaveNewPlayer(Entity entity, string passwordHash) { }
}
