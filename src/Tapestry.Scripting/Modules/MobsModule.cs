using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Registration;
using Tapestry.Scripting.Services;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class MobsModule : IJintApiModule
{
    private readonly ApiMobs _mobs;
    private readonly MobAIManager _mobAIManager;
    private readonly MobCommandRegistry _mobCommandRegistry;
    private readonly MobCommandQueue _mobCommandQueue;
    private readonly CommandRegistry _commandRegistry;
    private readonly RegistrationPolicy _registrationPolicy;
    private readonly Dictionary<string, (string Pack, JsValue Hooks)> _mobScriptRegistry = new();
    private readonly ILogger<MobsModule> _logger;

    public MobsModule(ApiMobs mobs, MobAIManager mobAIManager,
        MobCommandRegistry mobCommandRegistry, MobCommandQueue mobCommandQueue,
        CommandRegistry commandRegistry,
        RegistrationPolicy registrationPolicy,
        ILogger<MobsModule> logger)
    {
        _mobs = mobs;
        _mobAIManager = mobAIManager;
        _mobCommandRegistry = mobCommandRegistry;
        _mobCommandQueue = mobCommandQueue;
        _commandRegistry = commandRegistry;
        _registrationPolicy = registrationPolicy;
        _logger = logger;
    }

    public string Namespace => "mobs";

    public object Build(JintEngine engine)
    {
        return new
        {
            registerBehavior = new Action<string, JsValue>((name, handler) =>
            {
                var packName = engine.GetValue("__currentPack").ToString();
                _mobAIManager.RegisterBehavior(name, ctx =>
                {
                    var contextObj = new
                    {
                        entityId = ctx.EntityId.ToString(),
                        name = ctx.Name,
                        roomId = ctx.RoomId,
                        behavior = ctx.Behavior
                    };
                    engine.InvokeAsPack(packName, handler, JsValue.FromObject(engine, contextObj));
                });
            }),

            registerCommand = new Action<string, JsValue>((verb, options) =>
            {
                var packName = engine.GetValue("__currentPack").ToString();

                var sourceFileVal = engine.GetValue("__currentSource");
                var sourceFile = (sourceFileVal.Type != Types.Undefined && sourceFileVal.Type != Types.Null)
                    ? sourceFileVal.ToString()
                    : "";

                var optObj = (ObjectInstance)options;
                var handler = optObj.Get("handler");
                var gmcpJs = optObj.Get("gmcp");

                // Jint 4.7.1 has no IsBoolean; a missing JS field marshals to CLR null. Read via Type==Boolean.
                var overrideVal = optObj.Get("override");
                bool isOverride = overrideVal.Type == Types.Boolean && (bool)overrideVal.ToObject()!;

                string? gmcpChannel = null;
                var prependSender = false;
                if (gmcpJs.Type != Types.Undefined && gmcpJs.Type != Types.Null)
                {
                    var gmcpObj = (ObjectInstance)gmcpJs;
                    var channelJs = gmcpObj.Get("channel");
                    gmcpChannel = (channelJs.Type != Types.Undefined && channelJs.Type != Types.Null)
                        ? channelJs.ToString()
                        : null;
                    var prependJs = gmcpObj.Get("prependSender");
                    prependSender = prependJs.Type == Types.Boolean && (bool)prependJs.ToObject()!;
                }

                // Declarative: accumulate a candidate; both registry writes replay at Resolve()
                // (the seal barrier). Kind "mob-command" is disjoint from "command" -- core
                // legitimately registers a player `say` (commands.register) AND a mob `say`
                // (mobs.registerCommand); same kind would self-collide at boot (tapestry#98).
                var verbKey = verb.ToLower();
                _registrationPolicy.Record(new RegistrationCandidate(
                    Kind: "mob-command",
                    Name: verbKey,
                    Owner: packName,
                    IsOverride: isOverride,
                    Commit: () =>
                    {
                        // Legacy path: keep in MobCommandRegistry for backwards compat
                        _mobCommandRegistry.Register(verbKey, new MobCommandRegistration
                        {
                            Handler = (mob, text) =>
                            {
                                var mobObj = new
                                {
                                    entityId = mob.EntityId.ToString(),
                                    name = mob.Name,
                                    roomId = mob.RoomId
                                };
                                engine.InvokeAsPack(packName, handler, JsValue.FromObject(engine, mobObj), JsValue.FromObject(engine, text));
                            },
                            GmcpChannel = gmcpChannel,
                            PrependSender = prependSender
                        });

                        // Unified path: also register in CommandRegistry with roles: ["mob"]
                        _commandRegistry.Register(
                            verbKey,
                            actorCtx =>
                            {
                                var mobObj = new
                                {
                                    entityId = actorCtx.EntityId.ToString(),
                                    name = actorCtx.Name,
                                    roomId = actorCtx.RoomId
                                };
                                var text = string.Join(" ", actorCtx.RawArgs);
                                try
                                {
                                    engine.InvokeAsPack(packName, handler, JsValue.FromObject(engine, mobObj), JsValue.FromObject(engine, text));
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Mob command '{Verb}' dispatch error", verb);
                                }
                            },
                            roles: ["mob"]
                        );
                    },
                    SourceFile: sourceFile,
                    Line: 0));
            }),

            command = new Action<string, string, JsValue>((entityIdStr, commandStr, delayJs) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return; }

                var baseDelay = (delayJs != null && delayJs.Type != Types.Undefined && delayJs.Type != Types.Null)
                    ? (double)delayJs.ToObject()!
                    : 0.0;

                // Chain support: each semicolon-separated segment queued with 0 additional delay
                var segments = commandStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var segment in segments)
                {
                    var trimmed = segment.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        _mobCommandQueue.Enqueue(entityId, trimmed, baseDelay);
                        baseDelay = 0.0; // subsequent segments chain after the first
                    }
                }
            }),

            registerScript = new Action<string, JsValue>((templateId, hooks) =>
            {
                var packName = engine.GetValue("__currentPack").ToString();
                _mobScriptRegistry[templateId] = (packName, hooks);
            }),

            invokeHook = new Action<string, string, JsValue, JsValue, JsValue>(
                (templateId, hookName, mobObj, playerObj, extraArg) =>
            {
                if (!_mobScriptRegistry.TryGetValue(templateId, out var script))
                {
                    return;
                }
                var hooksObj = (ObjectInstance)script.Hooks;
                var fn = hooksObj.Get(hookName);
                if (fn.Type == Types.Undefined || fn.Type == Types.Null)
                {
                    return;
                }
                try
                {
                    engine.InvokeAsPack(script.Pack, fn, mobObj, playerObj, extraArg);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Mob script error: template={TemplateId} hook={Hook}",
                        templateId, hookName);
                }
            }),

            getProperties = new Func<string, Dictionary<string, object?>?>(_mobs.GetEntityProperties),
            getTicksSinceLastAction = new Func<string, long>(_mobs.GetMobTicksSinceLastAction),
            recordAction = new Action<string>(_mobs.RecordMobAction),
            spawnMob = new Func<string, string, object?>(_mobs.SpawnMob)
        };
    }
}
