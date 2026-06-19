using Jint.Runtime.Modules;
using Tapestry.Engine.Registration;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Interop;

/// <summary>
/// The Tapestry ES-module resolver. Carries the three things native import does not:
/// (1) source resolution (@tapestry/engine -> the builder module; relative -> within
/// the importing pack; bare scoped -> the target pack's entry); (2) the declared
/// dependency-edge gate at resolve time (required edges hard, optional edges soft);
/// (3) stable pack-qualified module identity so GetActivePack() can map a location
/// back to its pack. Edges and pack directories are supplied at boot via Build(),
/// mirroring PackDependencyGraph.Build().
/// </summary>
public sealed class TapestryModuleLoader : ModuleLoader
{
    private const string EngineSpecifier = "@tapestry/engine";
    private const string EntryRelFile = "dist/scripts/index.js";

    private readonly IPackEdgeOracle _edges;
    private IReadOnlyDictionary<string, string> _packDirs = new Dictionary<string, string>();
    private ISet<(string from, string to)> _optionalEdges = new HashSet<(string, string)>();

    public TapestryModuleLoader(IPackEdgeOracle edges)
    {
        _edges = edges;
    }

    /// <summary>Boot-time population (called from ContentLoadingModule alongside the dep graph).</summary>
    public void Build(IReadOnlyDictionary<string, string> packDirs, ISet<(string from, string to)> optionalEdges)
    {
        _packDirs = packDirs;
        _optionalEdges = optionalEdges;
    }

    public string ModuleKey(string ns, string relFile) => $"pack:{ns}::{relFile}";

    public static string? PackOf(string? location)
    {
        if (string.IsNullOrEmpty(location) || !location!.StartsWith("pack:", StringComparison.Ordinal))
        {
            return null;
        }
        var rest = location.Substring("pack:".Length);
        var sep = rest.IndexOf("::", StringComparison.Ordinal);
        return sep < 0 ? rest : rest.Substring(0, sep);
    }

    public static string? SourceOf(string? location)
    {
        if (string.IsNullOrEmpty(location) || !location!.StartsWith("pack:", StringComparison.Ordinal))
        {
            return null;
        }
        var sep = location.IndexOf("::", StringComparison.Ordinal);
        return sep < 0 ? null : location.Substring(sep + 2);
    }

    public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
    {
        var spec = moduleRequest.Specifier;

        // The engine drives each top-level pack-file import with the canonical key
        // (pack:<ns>::<relFile>), and the optional-absent stub key is empty:<ns>. Both are
        // already resolved identities - pass straight through, else they would fall into the
        // bare-scoped branch and be mangled by PackNamespace (the key contains '/' and '::').
        if (spec.StartsWith("pack:", StringComparison.Ordinal) || spec.StartsWith("empty:", StringComparison.Ordinal))
        {
            return new ResolvedSpecifier(moduleRequest, spec, null, SpecifierType.Bare);
        }

        if (spec == EngineSpecifier)
        {
            // The shared builder module (registered via engine.Modules.Add), matched by this key.
            return new ResolvedSpecifier(moduleRequest, EngineSpecifier, null, SpecifierType.Bare);
        }

        var importerNs = PackOf(referencingModuleLocation);

        // Relative sibling import: intra-pack, always allowed.
        if (spec.StartsWith("./", StringComparison.Ordinal) || spec.StartsWith("../", StringComparison.Ordinal))
        {
            if (importerNs is null)
            {
                throw new InvalidOperationException($"relative import '{spec}' from a non-pack module");
            }
            var importerRel = SourceOf(referencingModuleLocation) ?? "";
            var resolvedRel = ResolveRelative(importerRel, spec);
            return new ResolvedSpecifier(moduleRequest, ModuleKey(importerNs, resolvedRel), null, SpecifierType.Bare);
        }

        // Bare scoped specifier: cross-pack import.
        var targetNs = PackLoader.PackNamespace(spec);
        if (importerNs is not null && !string.Equals(importerNs, targetNs, StringComparison.OrdinalIgnoreCase))
        {
            if (!_edges.DeclaresEdge(importerNs, targetNs))
            {
                throw new InvalidOperationException(
                    $"{importerNs} ({SourceOf(referencingModuleLocation)}): imports '{spec}' " +
                    $"but has no declared dependency on '{targetNs}'; add it to dependencies or optional_dependencies");
            }
            if (!_packDirs.ContainsKey(targetNs))
            {
                if (_optionalEdges.Contains((importerNs, targetNs)))
                {
                    // Declared-optional target absent this boot: resolve to an empty module so a
                    // `import * as ns` + capability guard sees no exports. First-class optional interop.
                    return new ResolvedSpecifier(moduleRequest, $"empty:{targetNs}", null, SpecifierType.Bare);
                }
                throw new InvalidOperationException(
                    $"{importerNs} ({SourceOf(referencingModuleLocation)}): requires '{targetNs}' which is not loaded");
            }
        }

        return new ResolvedSpecifier(moduleRequest, ModuleKey(targetNs, EntryRelFile), null, SpecifierType.Bare);
    }

    protected override string LoadModuleContents(JintEngine engine, ResolvedSpecifier resolved)
    {
        var key = resolved.Key;

        if (key.StartsWith("empty:", StringComparison.Ordinal))
        {
            return "export {};\n";
        }

        if (key.StartsWith("pack:", StringComparison.Ordinal))
        {
            var ns = PackOf(key)!;
            var rel = SourceOf(key)!;
            if (!_packDirs.TryGetValue(ns, out var dir))
            {
                throw new FileNotFoundException($"pack directory for '{ns}' not registered");
            }
            var path = Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"compiled module not found: {path}");
            }
            return File.ReadAllText(path);
        }

        throw new InvalidOperationException($"unexpected module key: {key}");
    }

    // Resolve "./x.js" / "../y/z.js" against the importer's relative path (forward slashes).
    private static string ResolveRelative(string importerRel, string spec)
    {
        var baseDir = importerRel.Contains('/')
            ? importerRel.Substring(0, importerRel.LastIndexOf('/'))
            : "";
        var segments = new List<string>(baseDir.Length == 0 ? Array.Empty<string>() : baseDir.Split('/'));
        foreach (var part in spec.Split('/'))
        {
            if (part == "." || part.Length == 0)
            {
                continue;
            }
            if (part == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }
                continue;
            }
            segments.Add(part);
        }
        return string.Join('/', segments);
    }
}
