using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Engine.Help;
using Tapestry.Engine.Registration;
using Tapestry.Engine.Stats;
using Tapestry.Scripting.Services;
using Tapestry.Shared;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class CommandsModule : IJintApiModule
{
    private readonly CommandRegistry _commandRegistry;
    private readonly ApiMessaging _messaging;
    private readonly ApiWorld _worldOps;
    private readonly ApiStats _stats;
    private readonly World _world;
    private readonly ILogger<CommandsModule> _logger;
    private readonly CommandResponseContext _responseContext;
    private readonly EventBus _eventBus;
    private readonly ArgResolver _argResolver;
    private readonly RegistrationPolicy _registrationPolicy;
    private readonly HelpService _helpService;


    public CommandsModule(
        CommandRegistry commandRegistry,
        ApiMessaging messaging,
        ApiWorld worldOps,
        ApiStats stats,
        World world,
        ILogger<CommandsModule> logger,
        CommandResponseContext responseContext,
        EventBus eventBus,
        ArgResolver argResolver,
        RegistrationPolicy registrationPolicy,
        HelpService helpService)
    {
        _commandRegistry = commandRegistry;
        _messaging = messaging;
        _worldOps = worldOps;
        _stats = stats;
        _world = world;
        _logger = logger;
        _responseContext = responseContext;
        _eventBus = eventBus;
        _argResolver = argResolver;
        _registrationPolicy = registrationPolicy;
        _helpService = helpService;
    }

    public string Namespace => "commands";

    public object Build(JintEngine engine)
    {
        return new
        {
            register = new Action<JsValue>(definition =>
            {
                RegisterCommand(engine, definition);
            }),

            listForPlayer = new Func<string, object[]>(entityIdStr =>
            {
                return ListForPlayer(entityIdStr);
            }),

            categories = new Func<object[]>(() =>
                _helpService.VisibleDeclaredCategories
                    .Select(c => (object)new { id = c.Id, label = c.Label })
                    .ToArray()),

            unregister = new Action<string>((commandName) =>
            {
                _commandRegistry.Unregister(commandName);
            })
        };
    }


    private static ArgDefinition? ParseArgDefinition(JsValue? value)
    {
        if (value == null || value.Type == Types.Undefined || value.Type == Types.Null) { return null; }

        if (value.Type == Types.String)
        {
            return new ArgDefinition { Type = value.ToString(), Required = true };
        }

        if (value is not ObjectInstance defObj) { return null; }

        var typeVal = defObj.Get("type");
        var requiredVal = defObj.Get("required");
        var bulkVal = defObj.Get("bulk");
        var prepsVal = defObj.Get("prepositions");

        var type = typeVal.Type != Types.Undefined ? typeVal.ToString() : "keyword";
        var required = requiredVal.Type != Types.Boolean || (bool)requiredVal.ToObject()!;
        var bulk = bulkVal.Type == Types.Boolean && (bool)bulkVal.ToObject()!;

        string[] prepositions = [];
        if (prepsVal is JsArray prepsArray)
        {
            prepositions = new string[prepsArray.Length];
            for (uint i = 0; i < prepsArray.Length; i++)
            {
                prepositions[i] = prepsArray[i].ToString();
            }
        }

        var bypassVal = defObj.Get("bypass_visibility");
        var bypassVisibility = bypassVal.Type == Types.Boolean && (bool)bypassVal.ToObject()!;

        return new ArgDefinition { Type = type, Required = required, Bulk = bulk, Prepositions = prepositions, BypassVisibility = bypassVisibility };
    }

    private void RegisterCommand(JintEngine engine, JsValue definition)
    {
        var obj = (ObjectInstance)definition;
        var name = obj.Get("name").ToString();
        var handler = obj.Get("handler");
        var priorityVal = obj.Get("priority");
        var priority = priorityVal.Type == Types.Number ? (int)(double)priorityVal.ToObject()! : 0;

        // Jint 4.7.1 has no IsBoolean; a missing JS field marshals to CLR null. Read via Type==Boolean.
        var overrideVal = obj.Get("override");
        bool isOverride = overrideVal.Type == Types.Boolean && (bool)overrideVal.ToObject()!;

        string[] aliases = [];
        var aliasVal = obj.Get("aliases");
        if (aliasVal is JsArray aliasArray)
        {
            aliases = new string[aliasArray.Length];
            for (uint i = 0; i < aliasArray.Length; i++)
            {
                aliases[i] = aliasArray[i].ToString();
            }
        }

        var packName = engine.CurrentPackOwner();
        var sourceFile = engine.CurrentSourceFile();

        // Parse roles: ["player", "mob"] -- defaults to ["player"] if absent
        var rolesVal = obj.Get("roles");
        string[] roles = ["player"];
        if (rolesVal is JsArray rolesArray)
        {
            roles = new string[rolesArray.Length];
            for (uint i = 0; i < rolesArray.Length; i++)
            {
                roles[i] = rolesArray[i].ToString();
            }
        }

        // Parse args: { item: { type: 'inventory', required: true }, ... }
        Dictionary<string, ArgDefinition>? argDefinitions = null;
        var argsVal = obj.Get("args");
        if (argsVal is ObjectInstance argsObj)
        {
            argDefinitions = new Dictionary<string, ArgDefinition>();
            foreach (var prop in argsObj.GetOwnProperties())
            {
                var argDef = ParseArgDefinition(prop.Value.Value);
                if (argDef != null) { argDefinitions[prop.Key.ToString()] = argDef; }
            }
        }

        // Parse gmcp: false | { channel: 'say', prependSender: false }
        GmcpConfig? gmcpConfig = null;
        var gmcpVal = obj.Get("gmcp");
        if (gmcpVal.Type == Types.Boolean && !(bool)gmcpVal.ToObject()!)
        {
            gmcpConfig = new GmcpConfig { Disabled = true };
        }
        else if (gmcpVal is ObjectInstance gmcpObj)
        {
            var channelJs = gmcpObj.Get("channel");
            var prependJs = gmcpObj.Get("prependSender");
            gmcpConfig = new GmcpConfig
            {
                Channel = channelJs.Type != Types.Undefined ? channelJs.ToString() : null,
                PrependSender = prependJs.Type == Types.Boolean && (bool)prependJs.ToObject()!
            };
        }

        // admin: true shorthand -- wins over explicit visibleTo
        var adminVal = obj.Get("admin");
        var isAdmin = adminVal.Type == Types.Boolean && (bool)adminVal.ToObject()!;

        Func<Entity, bool>? visibleTo = null;
        if (isAdmin)
        {
            var visibleToExplicit = obj.Get("visibleTo");
            if (visibleToExplicit.Type != Types.Undefined && visibleToExplicit.Type != Types.Null)
            {
                _logger.LogWarning(
                    "Command '{Name}' has both admin: true and visibleTo -- admin: true wins, visibleTo ignored.",
                    name);
            }
            visibleTo = entity => entity.HasRole("admin");
        }
        else
        {
            var visibleToVal = obj.Get("visibleTo");
            if (visibleToVal.Type != Types.Undefined && visibleToVal.Type != Types.Null)
            {
                var fn = visibleToVal;
                visibleTo = entity =>
                {
                    try
                    {
                        var playerObj = new
                        {
                            entityId = entity.Id.ToString(),
                            hasRole = new Func<string, bool>(role => entity.HasRole(role)),
                            hasTag = new Func<string, bool>(tag => entity.HasTag(tag))
                        };
                        // JintEngine is not thread-safe; visibleTo predicates share the singleton engine.
                        // visibleTo is a deferred pack predicate — attribute it to the registering pack.
                        var result = engine.InvokeAsPack(packName, fn, null, new object[] { playerObj });
                        return result.Type == Types.Boolean && (bool)result.ToObject()!;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "visibleTo predicate error for command '{Name}'", name);
                        return false;
                    }
                };
            }
        }

        if (isAdmin)
        {
            roles = ["admin"];
        }

        var capturedArgDefs = argDefinitions;
        var capturedGmcp = gmcpConfig;

        Action<ActorContext> actorHandler =
            actorCtx => { InvokeActorHandler(engine, handler, actorCtx, capturedArgDefs, capturedGmcp, packName); };

        // Declarative: accumulate a candidate. The real Register replays — with the identical
        // argument list — at Resolve() (the seal barrier), so a same-name command from two packs
        // is a boot error unless one declares { override: true } + a dependency edge on the owner.
        _registrationPolicy.Record(new RegistrationCandidate(
            Kind: "command",
            Name: name,
            Owner: packName,
            IsOverride: isOverride,
            Commit: () => _commandRegistry.Register(
                name,
                actorHandler,
                aliases,
                priority,
                packName,
                sourceFile,
                visibleTo,
                roles: roles,
                argDefinitions: argDefinitions,
                gmcp: gmcpConfig),
            SourceFile: sourceFile,
            Line: 0));
    }

    private object[] ListForPlayer(string entityIdStr)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId)) { return Array.Empty<object>(); }
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return Array.Empty<object>(); }

        var result = new List<object>();

        foreach (var keyword in _commandRegistry.PrimaryKeywords)
        {
            var reg = _commandRegistry.Resolve(keyword);
            if (reg == null) { continue; }
            // PrimaryKeywords already returns distinct keywords; dedup not needed.

            if (reg.VisibleTo != null)
            {
                bool visible;
                try { visible = reg.VisibleTo(entity); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "visibleTo error for '{Keyword}'", reg.Keyword);
                    visible = false;
                }
                if (!visible) { continue; }
            }

            // Hidden category or hidden topic - decluttered from the catalog (still dispatchable, still help-able).
            if (!_helpService.IsListed(reg.Keyword)) { continue; }

            var topic = _helpService.GetTopicById(reg.Keyword);
            var category = topic?.Category ?? "";
            var description = topic?.Brief ?? "";

            result.Add(new
            {
                keyword = reg.Keyword,
                category = category,
                description = description,
                aliases = reg.Aliases
            });
        }

        return result.ToArray();
    }

    private void InvokeActorHandler(
        JintEngine engine, JsValue handler, ActorContext actorCtx,
        Dictionary<string, ArgDefinition>? argDefs, GmcpConfig? gmcpConfig, string packName)
    {
        var isMob = actorCtx.Source == "mob";
        var name = isMob
            ? actorCtx.Name
            : (_worldOps.GetEntityName(actorCtx.EntityId.ToString()) ?? "Unknown");
        var roomId = actorCtx.RoomId
            ?? _worldOps.GetEntityRoomId(actorCtx.EntityId.ToString())
            ?? "";

        // Resolve structured args or pass raw args array
        object argsToPass;
        if (argDefs != null && argDefs.Count > 0)
        {
            string? sendErrorMsg = null;
            Action<string>? sendError = isMob ? null : msg => { sendErrorMsg = msg; };

            var (success, resolved, _) = _argResolver.Resolve(
                actorCtx, argDefs, actorCtx.RawArgs, sendError);

            if (!success)
            {
                if (!isMob && sendErrorMsg != null)
                {
                    _messaging.Send(actorCtx.EntityId, sendErrorMsg);
                }
                return;
            }
            argsToPass = resolved;
        }
        else
        {
            argsToPass = actorCtx.RawArgs;
        }

        var actorObj = new
        {
            entityId = actorCtx.EntityId.ToString(),
            name = name,
            roomId = roomId,
            source = actorCtx.Source,
            stats = isMob ? null : _stats.GetEntityStats(actorCtx.EntityId.ToString()),
            send = isMob
                ? new Action<string>(_ => { })
                : new Action<string>(text => { _messaging.Send(actorCtx.EntityId, text); }),
            // Slice C: like send, but this write is NOT mirrored to watch spectators (a private DM).
            // The viewer pack's tell/reply override uses this for the sender's echo.
            sendPrivate = isMob
                ? new Action<string>(_ => { })
                : new Action<string>(text => { _messaging.SendPrivate(actorCtx.EntityId, text); }),
            sendToRoom = new Action<string>(text =>
            {
                if (!string.IsNullOrEmpty(roomId))
                {
                    _messaging.SendToRoomExcept(roomId, actorCtx.EntityId.ToString(), text);
                }
            }),
            hasRole = new Func<string, bool>(role =>
            {
                var entity = _world.GetEntity(actorCtx.EntityId);
                return entity?.HasRole(role) ?? false;
            }),
            hasTag = new Func<string, bool>(tag =>
            {
                var entity = _world.GetEntity(actorCtx.EntityId);
                return entity?.HasTag(tag) ?? false;
            })
        };

        try
        {
            engine.InvokeAsPack(packName, handler, null, new object[] { actorObj, argsToPass });
        }
        finally
        {
            if (!isMob) { _responseContext.Reset(actorCtx.EntityId); }
        }

        // GMCP auto-publish after every player command (unless disabled)
        if (gmcpConfig?.Disabled == true || isMob) { return; }

        var channel = gmcpConfig?.Channel ?? "feedback";
        _eventBus.Publish(new GameEvent
        {
            Type = "communication.message",
            SourceEntityId = actorCtx.EntityId,
            Data = { ["channel"] = channel, ["command"] = actorCtx.Command }
        });
    }
}
