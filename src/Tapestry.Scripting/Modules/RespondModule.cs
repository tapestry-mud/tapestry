using Jint.Native;
using Tapestry.Scripting.Services;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class RespondModule : IJintApiModule
{
    private readonly IGmcpModuleAdapter _gmcp;
    private readonly CommandResponseContext _responseContext;

    public RespondModule(IGmcpModuleAdapter gmcp, CommandResponseContext responseContext)
    {
        _gmcp = gmcp;
        _responseContext = responseContext;
    }

    public string Namespace => "respond";

    public object Build(JintEngine engine)
    {
        var gmcp = _gmcp;
        var ctx = _responseContext;

        var sendImpl = new Action<string, string, string, string>((entityIdStr, type, msg, cat) =>
        {
            if (!Guid.TryParse(entityIdStr, out var entityId))
            {
                return;
            }

            gmcp.Send(entityId, "Response.Feedback", new
            {
                status = "ok",
                type,
                message = msg,
                category = cat
            });
        });

        var suppressImpl = new Action<string>(entityIdStr =>
        {
            if (!Guid.TryParse(entityIdStr, out var entityId))
            {
                return;
            }

            ctx.Suppress(entityId);
        });

        // Store delegates on engine scope temporarily so the JS closure can capture them.
        // Use double-underscore names unlikely to collide with pack scripts.
        engine.SetValue("__respondSend__", sendImpl);
        engine.SetValue("__respondSuppress__", suppressImpl);

        var fn = engine.Evaluate("""
            (function() {
                var _send = __respondSend__;
                var _sup = __respondSuppress__;
                var respond = function(entityId, type, message, category) {
                    _send(entityId, type, message, category);
                };
                respond.suppress = function(entityId) { _sup(entityId); };
                return respond;
            })()
            """);

        // Null out temp globals so they don't persist in pack script scope.
        engine.SetValue("__respondSend__", JsValue.Null);
        engine.SetValue("__respondSuppress__", JsValue.Null);

        return fn;
    }
}
