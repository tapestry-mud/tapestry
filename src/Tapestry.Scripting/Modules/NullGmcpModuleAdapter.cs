namespace Tapestry.Scripting.Modules;

public class NullGmcpModuleAdapter : IGmcpModuleAdapter
{
    public void Send(Guid entityId, string package, object payload) { }

    public bool SupportsPackage(Guid entityId, string package)
    {
        return false;
    }

    public void Subscribe(string package, Action<Guid, object> callback) { }
}
