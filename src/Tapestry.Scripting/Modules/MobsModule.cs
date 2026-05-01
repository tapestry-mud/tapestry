using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Microsoft.Extensions.Logging;
using Tapestry.Engine.Mobs;
using Tapestry.Scripting.Services;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class MobsModule : IJintApiModule
{
    private readonly ApiMobs _mobs;
    private readonly MobAIManager _mobAIManager;
    private readonly MobCommandRegistry _mobCommandRegistry;
    private readonly MobCommandQueue _mobCommandQueue;
    private readonly Dictionary<string, JsValue> _mobScriptRegistry = new();
    private readonly ILogger<MobsModule> _logger;

    public MobsModule(ApiMobs mobs, MobAIManager mobAIManager,
        MobCommandRegistry mobCommandRegistry, MobCommandQueue mobCommandQueue,
        ILogger<MobsModule> logger)
    {
        _mobs = mobs;
        _mobAIManager = mobAIManager;
        _mobCommandRegistry = mobCommandRegistry;
        _mobCommandQueue = mobCommandQueue;
        _logger = logger;
    }

    public string Namespace => "mobs";

    public object Build(JintEngine engine)
    {
        return new
        {
            registerBehavior = new Action<string, JsValue>((name, handler) =>
            {
                _mobAIManager.RegisterBehavior(name, ctx =>
                {
                    var contextObj = new
                    {
                        entityId = ctx.EntityId.ToString(),
                        name = ctx.Name,
                        roomId = ctx.RoomId,
                        behavior = ctx.Behavior
                    };
                    engine.Invoke(handler, JsValue.FromObject(engine, contextObj));
                });
            }),

            registerCommand = new Action<string, JsValue>((verb, options) =>
            {
                var optObj = (ObjectInstance)options;
                var handler = optObj.Get("handler");
                var gmcpJs = optObj.Get("gmcp");

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

                _mobCommandRegistry.Register(verb.ToLower(), new MobCommandRegistration
                {
                    Handler = (mob, text) =>
                    {
                        var mobObj = new
                        {
                            entityId = mob.EntityId.ToString(),
                            name = mob.Name,
                            roomId = mob.RoomId
                        };
                        engine.Invoke(handler, JsValue.FromObject(engine, mobObj), text);
                    },
                    GmcpChannel = gmcpChannel,
                    PrependSender = prependSender
                });
            }),

            command = new Action<string, string, JsValue>((entityIdStr, commandStr, delayJs) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return;
                }
                var delay = (delayJs != null && delayJs.Type != Types.Undefined && delayJs.Type != Types.Null)
                    ? (double)delayJs.ToObject()!
                    : 0.0;
                _mobCommandQueue.Enqueue(entityId, commandStr, delay);
            }),

            registerScript = new Action<string, JsValue>((templateId, hooks) =>
            {
                _mobScriptRegistry[templateId] = hooks;
            }),

            invokeHook = new Action<string, string, JsValue, JsValue, JsValue>(
                (templateId, hookName, mobObj, playerObj, extraArg) =>
            {
                if (!_mobScriptRegistry.TryGetValue(templateId, out var hooks))
                {
                    return;
                }
                var hooksObj = (ObjectInstance)hooks;
                var fn = hooksObj.Get(hookName);
                if (fn.Type == Types.Undefined || fn.Type == Types.Null)
                {
                    return;
                }
                try
                {
                    engine.Invoke(fn, mobObj, playerObj, extraArg);
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
