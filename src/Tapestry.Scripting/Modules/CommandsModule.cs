using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Microsoft.Extensions.Logging;
using Tapestry.Engine;
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

    private readonly List<string> _undescribedCommands = new();

    public CommandsModule(
        CommandRegistry commandRegistry,
        ApiMessaging messaging,
        ApiWorld worldOps,
        ApiStats stats,
        World world,
        ILogger<CommandsModule> logger,
        CommandResponseContext responseContext)
    {
        _commandRegistry = commandRegistry;
        _messaging = messaging;
        _worldOps = worldOps;
        _stats = stats;
        _world = world;
        _logger = logger;
        _responseContext = responseContext;
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

            unregister = new Action<string>((commandName) =>
            {
                _commandRegistry.Unregister(commandName);
            })
        };
    }

    public void LogLoadTimeWarnings()
    {
        if (_undescribedCommands.Count == 0) { return; }
        _logger.LogWarning(
            "Commands registered without descriptions: {Commands}",
            string.Join(", ", _undescribedCommands));
        _undescribedCommands.Clear();
    }

    private void RegisterCommand(JintEngine engine, JsValue definition)
    {
        var obj = (ObjectInstance)definition;
        var name = obj.Get("name").ToString();
        var handler = obj.Get("handler");
        var priorityVal = obj.Get("priority");
        var priority = priorityVal.Type == Types.Number ? (int)(double)priorityVal.ToObject()! : 0;

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

        var packName = engine.GetValue("__currentPack").ToString();

        var sourceFileVal = engine.GetValue("__currentSource");
        var sourceFile = (sourceFileVal.Type != Types.Undefined && sourceFileVal.Type != Types.Null)
            ? sourceFileVal.ToString()
            : "";

        var descriptionVal = obj.Get("description");
        var description = (descriptionVal.Type != Types.Undefined && descriptionVal.Type != Types.Null)
            ? descriptionVal.ToString()
            : "";

        if (string.IsNullOrEmpty(description))
        {
            _undescribedCommands.Add(name);
        }

        var categoryVal = obj.Get("category");
        var category = (categoryVal.Type != Types.Undefined && categoryVal.Type != Types.Null)
            ? categoryVal.ToString()
            : "";

        // admin: true shorthand — wins over explicit visibleTo
        var adminVal = obj.Get("admin");
        var isAdmin = adminVal.Type == Types.Boolean && (bool)adminVal.ToObject()!;

        Func<Entity, bool>? visibleTo = null;
        if (isAdmin)
        {
            var visibleToExplicit = obj.Get("visibleTo");
            if (visibleToExplicit.Type != Types.Undefined && visibleToExplicit.Type != Types.Null)
            {
                _logger.LogWarning(
                    "Command '{Name}' has both admin: true and visibleTo — admin: true wins, visibleTo ignored.",
                    name);
            }
            visibleTo = entity => entity.HasTag("admin");
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
                            hasTag = new Func<string, bool>(tag => entity.HasTag(tag))
                        };
                        // JintEngine is not thread-safe; visibleTo predicates share the singleton engine.
                        var result = engine.Invoke(fn, null, new object[] { playerObj });
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

        _commandRegistry.Register(
            name,
            ctx => { InvokeCommandHandler(engine, handler, ctx); },
            aliases,
            priority,
            packName,
            description,
            category,
            sourceFile,
            visibleTo
        );
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

            var category = !string.IsNullOrEmpty(reg.Category)
                ? reg.Category
                : DeriveCategory(reg.SourceFile);

            result.Add(new
            {
                keyword = reg.Keyword,
                category = category,
                description = reg.Description
            });
        }

        return result.ToArray();
    }

    private static string DeriveCategory(string sourceFile)
    {
        if (string.IsNullOrEmpty(sourceFile)) { return "misc"; }
        var normalized = sourceFile.Replace('\\', '/');
        if (normalized.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["scripts/".Length..];
        }
        var lastSlash = normalized.LastIndexOf('/');
        if (lastSlash <= 0) { return "misc"; }
        var fileName = normalized[(lastSlash + 1)..];
        var dotIndex = fileName.LastIndexOf('.');
        var stem = dotIndex >= 0 ? fileName[..dotIndex] : fileName;
        return string.IsNullOrEmpty(stem) ? "misc" : stem.ToLower();
    }

    private void InvokeCommandHandler(JintEngine engine, JsValue handler, CommandContext ctx)
    {
        var name = _worldOps.GetEntityName(ctx.PlayerEntityId.ToString()) ?? "Unknown";
        var roomId = _worldOps.GetEntityRoomId(ctx.PlayerEntityId.ToString()) ?? "";
        var statsObj = _stats.GetEntityStats(ctx.PlayerEntityId.ToString());

        var playerObj = new
        {
            entityId = ctx.PlayerEntityId.ToString(),
            name = name,
            roomId = roomId,
            previousRoomId = roomId,
            stats = statsObj,
            isChargen = ctx.IsChargen,
            hasTag = new Func<string, bool>(tag =>
            {
                var entity = _world.GetEntity(ctx.PlayerEntityId);
                return entity?.HasTag(tag) ?? false;
            }),
            send = new Action<string>(text => { _messaging.Send(ctx.PlayerEntityId, text); }),
            sendToRoom = new Action<string>(text =>
            {
                if (!string.IsNullOrEmpty(roomId))
                {
                    _messaging.SendToRoomExcept(roomId, ctx.PlayerEntityId.ToString(), text);
                }
            })
        };

        try
        {
            engine.Invoke(handler, null, new object[] { playerObj, ctx.Args });
        }
        finally
        {
            _responseContext.Reset(ctx.PlayerEntityId);
        }
    }
}
