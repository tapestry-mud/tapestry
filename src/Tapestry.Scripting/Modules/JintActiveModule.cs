using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

using JintEngine = Jint.Engine;

namespace Tapestry.Scripting;

/// <summary>
/// Lexical pack attribution (model A). Jint's Engine.GetActiveScriptOrModule() is
/// internal in the pinned 4.9.3 (signed NuGet, no InternalsVisibleTo to us) AND its
/// return type Jint.Runtime.IScriptOrModule is itself internal - so the type cannot be
/// named here. We bind a compiled delegate that returns `object`, then read the public
/// `Location` getter off the boxed instance via a cached PropertyInfo (the spike's path).
/// </summary>
internal static class JintActiveModule
{
    private static readonly Func<JintEngine, object?> Accessor = BuildAccessor();
    // ConcurrentDictionary: tests exercise this from xUnit's parallel test runners, so the
    // per-type PropertyInfo cache must be thread-safe (a plain Dictionary raced under parallel
    // ActiveLocation calls and intermittently corrupted attribution). The engine itself is
    // single-threaded; this is purely to keep the cache safe under concurrent callers.
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> _locationPropByType = new();

    private static Func<JintEngine, object?> BuildAccessor()
    {
        var method = typeof(JintEngine).GetMethod(
            "GetActiveScriptOrModule",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is null)
        {
            throw new InvalidOperationException(
                "Jint.Engine.GetActiveScriptOrModule not found; the pinned Jint version changed.");
        }

        var dm = new DynamicMethod(
            "Tapestry_GetActiveScriptOrModule",
            typeof(object),
            new[] { typeof(JintEngine) },
            typeof(JintActiveModule),
            skipVisibility: true);
        var il = dm.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, method);  // returns Jint.Runtime.IScriptOrModule (internal), boxed as object
        il.Emit(OpCodes.Ret);
        return (Func<JintEngine, object?>)dm.CreateDelegate(typeof(Func<JintEngine, object?>));
    }

    public static string? ActiveLocation(JintEngine engine)
    {
        var active = Accessor(engine);
        if (active is null)
        {
            return null;
        }
        // IScriptOrModule is internal; read its public Location getter reflectively (cached per type).
        var prop = _locationPropByType.GetOrAdd(
            active.GetType(),
            t => t.GetProperty(
                "Location",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        return prop?.GetValue(active) as string;
    }
}
