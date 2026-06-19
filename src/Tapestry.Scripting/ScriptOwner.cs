using Tapestry.Scripting.Interop;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting;

/// <summary>
/// Owner/source attribution for registration capture. Attribution is purely lexical
/// (GetActivePack via the active ES module) after Phase J deleted the legacy Execute path.
/// </summary>
internal static class ScriptOwner
{
    public static string CurrentPackOwner(this JintEngine engine)
        => TapestryModuleLoader.PackOf(JintActiveModule.ActiveLocation(engine)) ?? "";

    public static string CurrentSourceFile(this JintEngine engine)
        => TapestryModuleLoader.SourceOf(JintActiveModule.ActiveLocation(engine)) ?? "";
}
