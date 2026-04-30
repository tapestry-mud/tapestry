using Jint.Native;
using Tapestry.Engine;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class GmcpModule : IJintApiModule
{
    private readonly IGmcpModuleAdapter _adapter;

    public GmcpModule(IGmcpModuleAdapter adapter)
    {
        _adapter = adapter;
    }

    public string Namespace => "gmcp";

    public object Build(JintEngine engine)
    {
        return new
        {
            send = new Action<string, string, JsValue>((entityIdStr, package, payloadJs) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return; }
                var payload = payloadJs.ToObject() ?? new object();
                _adapter.Send(entityId, package, payload);
            }),

            supports = new Func<string, string, bool>((entityIdStr, package) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return false; }
                return _adapter.SupportsPackage(entityId, package);
            }),

            on = new Action<string, JsValue>((package, callback) =>
            {
                _adapter.Subscribe(package, (entityId, data) =>
                {
                    try
                    {
                        var dataObj = JsValue.FromObject(engine, data);
                        engine.Invoke(callback, entityId.ToString(), dataObj);
                    }
                    catch
                    {
                        // Script callback errors should not crash the engine
                    }
                });
            })
        };
    }
}

public interface IGmcpModuleAdapter
{
    void Send(Guid entityId, string package, object payload);
    bool SupportsPackage(Guid entityId, string package);
    void Subscribe(string package, Action<Guid, object> callback);
}
