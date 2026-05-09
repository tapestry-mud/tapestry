using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class ArgsModule : IJintApiModule
{
    private readonly ArgResolver _argResolver;
    private readonly World _world;
    private readonly ILogger<ArgsModule> _logger;

    public string Namespace => "args";

    public ArgsModule(ArgResolver argResolver, World world, ILogger<ArgsModule> logger)
    {
        _argResolver = argResolver;
        _world = world;
        _logger = logger;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            registerType = new Action<string, JsValue>((name, jsResolver) =>
            {
                Func<ActorContext, ArgDefinition, string, (bool, object?, string?)> csharpResolver =
                    (actor, def, token) =>
                    {
                        try
                        {
                            var actorObj = new { entityId = actor.EntityId.ToString(), roomId = actor.RoomId ?? "" };
                            var defObj = new { type = def.Type, required = def.Required };
                            var result = engine.Invoke(jsResolver, null, new object[] { actorObj, token, defObj });

                            if (result is not ObjectInstance obj)
                            {
                                return (false, null, "Arg resolver returned non-object.\r\n");
                            }

                            var successVal = obj.Get("success");
                            var valueVal = obj.Get("value");
                            var errorVal = obj.Get("error");

                            var success = successVal.Type == Types.Boolean && (bool)successVal.ToObject()!;
                            var errorStr = errorVal.Type == Types.String ? errorVal.ToString() : null;
                            return (success, success ? valueVal.ToObject() : null, errorStr);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Pack arg resolver '{Name}' threw an exception", name);
                            return (false, null, "Resolver error.\r\n");
                        }
                    };

                _argResolver.RegisterPackType(name, csharpResolver);
            }),

            resolve = new Func<string, string, string, object?>((actorIdStr, token, typeName) =>
            {
                if (!Guid.TryParse(actorIdStr, out var entityId))
                {
                    return null;
                }
                var entity = _world.GetEntity(entityId);
                if (entity == null)
                {
                    return null;
                }
                var actor = new ActorContext { EntityId = entityId, RoomId = entity.LocationRoomId };
                var def = new ArgDefinition { Type = typeName, Required = true };
                var (success, value, _) = _argResolver.ResolveToken(actor, "target", def, token);
                return success ? value : null;
            }),

            matchesInput = new Func<string, string, bool>((entityIdStr, token) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id))
                {
                    return false;
                }
                var entity = _world.GetEntity(id);
                return entity != null && ArgResolver.MatchesInput(entity, token);
            })
        };
    }
}
