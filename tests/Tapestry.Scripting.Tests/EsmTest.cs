using System.IO;
using System.Runtime.CompilerServices;
using Jint.Native;
using Tapestry.Scripting.Interop;

namespace Tapestry.Scripting.Tests;

/// <summary>
/// Loads test JS as native ES modules (the replacement for the deleted legacy
/// Execute(script, packName) path). Attribution is lexical: a module loaded under
/// packName registers as packName (via the active module). Edges for the seal flow
/// through the DI PackDependencyGraph (BuildRuntime's graph.Build(deps)), NOT here.
/// </summary>
internal static class EsmTest
{
    private sealed class State
    {
        public readonly Dictionary<string, string> PackDirs = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<(string from, string to)> OptionalEdges = new();
        public int Counter;
    }

    private static readonly ConditionalWeakTable<TapestryModuleLoader, State> States = new();

    private static (TapestryModuleLoader loader, State st) Resolve(JintRuntime rt)
    {
        var loader = rt.Loader ?? throw new InvalidOperationException("EsmTest requires a runtime with an ESM loader");
        return (loader, States.GetValue(loader, _ => new State()));
    }

    private static string EnsurePackDir(State st, string packName)
    {
        if (!st.PackDirs.TryGetValue(packName, out var dir))
        {
            dir = Directory.CreateTempSubdirectory("esmtest-").FullName;
            Directory.CreateDirectory(Path.Combine(dir, "dist", "scripts"));
            st.PackDirs[packName] = dir;
        }
        return dir;
    }

    private static void WriteModule(string dir, string rel, string contents)
    {
        var full = Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, contents);
    }

    /// <summary>Load a JS body as an ESM module attributed to packName (replaces Execute(script, packName[, sourceFile])).</summary>
    public static void Load(JintRuntime rt, string packName, string jsBody, string? sourceFile = null)
    {
        var (loader, st) = Resolve(rt);
        var dir = EnsurePackDir(st, packName);
        // When a sourceFile is given, use it as the module's relative path so the registration's
        // captured source file (SourceOf the active module location) matches what the legacy
        // Execute(script, pack, sourceFile) recorded. Otherwise use a unique synthetic path.
        var rel = sourceFile ?? $"dist/scripts/__t{st.Counter++}.js";
        WriteModule(dir, rel, "import * as tapestry from \"@tapestry/engine\";\n" + jsBody);
        loader.Build(st.PackDirs, st.OptionalEdges);
        rt.ImportModule(rt.ModuleKey(packName, rel));
    }

    /// <summary>Evaluate a tapestry.* expression against @tapestry/engine; returns object? like rt.Evaluate.</summary>
    public static object? Eval(JintRuntime rt, string expr)
    {
        var (loader, st) = Resolve(rt);
        var dir = EnsurePackDir(st, "__eval");
        // Tolerate a trailing semicolon/whitespace on the expression (callers migrated from
        // rt.Evaluate often carry one); it is wrapped as `export const __result = (<expr>);`.
        var trimmed = expr.Trim();
        if (trimmed.EndsWith(";", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 1).TrimEnd();
        }
        var rel = $"dist/scripts/__e{st.Counter++}.js";
        WriteModule(dir, rel, "import * as tapestry from \"@tapestry/engine\";\nexport const __result = (" + trimmed + ");\n");
        loader.Build(st.PackDirs, st.OptionalEdges);
        var val = rt.ImportModuleAndGet(rt.ModuleKey("__eval", rel), "__result");
        if (val is null || val == JsValue.Null || val == JsValue.Undefined)
        {
            return null;
        }
        return val.ToObject();
    }
}
