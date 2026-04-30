using Tapestry.Engine;
using Tapestry.Engine.Rest;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class RestModule : IJintApiModule
{
    private readonly RestService _rest;

    public RestModule(RestService rest)
    {
        _rest = rest;
    }

    public string Namespace => "rest";

    public object Build(JintEngine engine)
    {
        return new
        {
            getRestState = new Func<string, string>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return "awake"; }
                return _rest.GetRestState(entityId);
            }),

            setRestState = new Func<string, string, string?, object>((entityIdStr, newState, furnitureIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return new { success = false, reason = "invalid_id" };
                }
                Guid? furnitureId = null;
                if (furnitureIdStr != null && Guid.TryParse(furnitureIdStr, out var fid))
                {
                    furnitureId = fid;
                }
                var (success, reason) = _rest.SetRestState(entityId, newState, furnitureId);
                return new { success, reason = reason ?? "success" };
            })
        };
    }
}
