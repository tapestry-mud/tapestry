using Jint.Native;
using Jint.Runtime;
using Tapestry.Scripting.Interop;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting;

/// <summary>
/// Owner/source attribution for registration capture. During the ESM migration both
/// loaders coexist: prefer the lexically active module's pack (ESM, model A); fall back
/// to the legacy __currentPack/__currentSource globals (legacy Execute packs). The
/// fallback is removed at Phase J when the legacy path is deleted.
/// </summary>
internal static class ScriptOwner
{
    public static string CurrentPackOwner(this JintEngine engine)
    {
        var active = TapestryModuleLoader.PackOf(JintActiveModule.ActiveLocation(engine));
        if (active is not null)
        {
            return active;
        }
        var legacy = engine.GetValue("__currentPack");
        return (legacy.Type != Types.Undefined && legacy.Type != Types.Null) ? legacy.ToString() : "";
    }

    public static string CurrentSourceFile(this JintEngine engine)
    {
        var active = TapestryModuleLoader.SourceOf(JintActiveModule.ActiveLocation(engine));
        if (active is not null)
        {
            return active;
        }
        var legacy = engine.GetValue("__currentSource");
        return (legacy.Type != Types.Undefined && legacy.Type != Types.Null) ? legacy.ToString() : "";
    }
}
