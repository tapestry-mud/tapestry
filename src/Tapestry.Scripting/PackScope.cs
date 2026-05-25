using Jint.Native;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting;

/// <summary>
/// Runs a deferred handler invocation with __currentPack set to the pack that
/// registered the handler, restoring the previous value afterward. Pack-registered
/// handlers (commands, events, schedules, abilities, flows) are invoked long after
/// load time, when the global __currentPack is otherwise stale (it holds the
/// last-loaded pack). Restoring (not just setting) keeps nested cross-pack
/// invocations correctly attributed.
/// </summary>
internal static class PackScope
{
    public static JsValue InvokeAsPack(this JintEngine engine, string? packName, JsValue handler, object? thisObj, object?[] args)
    {
        var prev = engine.GetValue("__currentPack");
        engine.SetValue("__currentPack", packName ?? "");
        try
        {
            return engine.Invoke(handler, thisObj, args);
        }
        finally
        {
            engine.SetValue("__currentPack", prev);
        }
    }

    // Overload matching the no-thisObj call shape used by Events/Schedule modules.
    public static JsValue InvokeAsPack(this JintEngine engine, string? packName, JsValue handler, params JsValue[] args)
    {
        var prev = engine.GetValue("__currentPack");
        engine.SetValue("__currentPack", packName ?? "");
        try
        {
            return engine.Invoke(handler, args);
        }
        finally
        {
            engine.SetValue("__currentPack", prev);
        }
    }
}
