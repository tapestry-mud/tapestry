using Tapestry.Engine.Consequence;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class ConsequenceModule : IJintApiModule
{
    private readonly ConsequenceOverlay _overlay;

    public string Namespace => "consequence";

    public ConsequenceModule(ConsequenceOverlay overlay)
    {
        _overlay = overlay;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            stamp = new Func<string, string, string, bool>((roomId, kind, lifespan) =>
            {
                if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(kind))
                {
                    return false;
                }
                _overlay.Stamp(roomId, kind, string.IsNullOrEmpty(lifespan) ? "ephemeral" : lifespan);
                return true;
            }),
            list = new Func<string, object>(roomId => _overlay.List(roomId)
                .Select(e => (object)new { kind = e.Kind, lifespan = e.Lifespan })
                .ToArray()),
            has = new Func<string, string, bool>((roomId, kind) => _overlay.Has(roomId, kind)),
            clear = new Func<string, bool>(roomId => _overlay.ClearRoom(roomId)),
            collapseExit = new Func<string, string, bool>((roomId, direction) =>
                _overlay.CollapseExit(roomId, direction)),
        };
    }
}
