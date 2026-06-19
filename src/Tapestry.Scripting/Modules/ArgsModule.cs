using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Engine.Registration;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class ArgsModule : IJintApiModule
{
    private readonly ArgResolver _argResolver;
    private readonly World _world;
    private readonly ILogger<ArgsModule> _logger;
    private readonly RegistrationPolicy _registrationPolicy;

    public string Namespace => "args";

    public ArgsModule(ArgResolver argResolver, World world, ILogger<ArgsModule> logger, RegistrationPolicy registrationPolicy)
    {
        _argResolver = argResolver;
        _world = world;
        _logger = logger;
        _registrationPolicy = registrationPolicy;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            registerType = new Action<JsValue>(def =>
            {
                if (def is not ObjectInstance defObjShape)
                {
                    throw new InvalidOperationException(
                        "tapestry.args.registerType now takes a definition object: " +
                        "registerType({ name, resolve, override }). The positional (name, fn) form was removed.");
                }

                var name = defObjShape.Get("name").ToString();
                var jsResolver = defObjShape.Get("resolve");
                if (jsResolver.Type != Types.Object)
                {
                    throw new InvalidOperationException(
                        $"tapestry.args.registerType('{name}') requires a 'resolve' function.");
                }

                // Jint 4.7.1 has no IsBoolean; a missing JS field marshals to CLR null. Read via Type==Boolean.
                var ov = defObjShape.Get("override");
                bool isOverride = ov.Type == Types.Boolean && (bool)ov.ToObject()!;

                // Owner is the pack namespace (e.g. "tapestry-tinkers") -- the same key the
                // dependency-edge gate uses, so an edge-gated override actually matches.
                var owner = engine.CurrentPackOwner();
                var sourceFile = engine.CurrentSourceFile();

                Func<ActorContext, ArgDefinition, string, (bool, object?, string?)> csharpResolver =
                    (actor, argDef, token) =>
                    {
                        try
                        {
                            var actorObj = new { entityId = actor.EntityId.ToString(), roomId = actor.RoomId ?? "" };
                            var resolverDefObj = new { type = argDef.Type, required = argDef.Required };
                            var result = engine.InvokeAsPack(owner, jsResolver, null, new object[] { actorObj, token, resolverDefObj });

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

                // Declarative: accumulate a candidate. The real RegisterPackType replays -- with the
                // identical args -- at Resolve() (the seal). Two packs registering the same arg type
                // is a boot error unless one declares { override: true } + a dependency edge on the owner.
                _registrationPolicy.Record(new RegistrationCandidate(
                    Kind: "arg-type",
                    Name: name,
                    Owner: owner,
                    IsOverride: isOverride,
                    Commit: () => _argResolver.RegisterPackType(name, owner, csharpResolver),
                    SourceFile: sourceFile,
                    Line: 0));
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
