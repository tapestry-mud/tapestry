using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Scripting.Authoring;

/// <summary>
/// Boot-time scanner that re-registers frozen oracle table side-cars from the authoring root.
/// Mirrors <see cref="AuthoredAreaLoader"/>: scans <c>&lt;root&gt;/**/*-oracle-table.yaml</c>
/// and <c>&lt;root&gt;/**/places-oracle.yaml</c>, loads each via
/// <see cref="YamlContentLoader.LoadOracleTable"/>, derives the area id from the path, and
/// registers the table under the canonical <c>&lt;areaId&gt;:&lt;kind&gt;</c> key.
///
/// This is the reload half of T4 (same-session register): T4 keeps tables live during a
/// session; T6 restores them after a reboot from the authored root (data/areas/**) which
/// pack oracle: globs do not reach.
/// </summary>
public class AuthoredOracleLoader
{
    private readonly string _root;
    private readonly OracleTableRegistry _registry;
    private readonly ILogger<AuthoredOracleLoader>? _logger;

    public AuthoredOracleLoader(string root, OracleTableRegistry registry, ILogger<AuthoredOracleLoader>? logger = null)
    {
        _root = root;
        _registry = registry;
        _logger = logger;
    }

    public void Load()
    {
        if (!Directory.Exists(_root))
        {
            _logger?.LogDebug("Authored oracle tables root {Root} missing; nothing to load.", _root);
            return;
        }

        var files = Directory.EnumerateFiles(_root, "*.yaml", SearchOption.AllDirectories)
            .Where(f =>
            {
                var name = System.IO.Path.GetFileName(f);
                return name.EndsWith("-oracle-table.yaml", StringComparison.Ordinal)
                    || string.Equals(name, "places-oracle.yaml", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(f => f)
            .ToList();

        var loaded = 0;
        var warnings = 0;

        foreach (var file in files)
        {
            try
            {
                var yaml = File.ReadAllText(file);
                var table = YamlContentLoader.LoadOracleTable(yaml);
                var areaId = AreaIdFromAuthoringPath(file);
                table.Id = OracleTable.OracleTableId(areaId, table.Kind);
                table.SourcePack = areaId;
                _registry.Register(table);
                loaded++;
                _logger?.LogDebug("  Authored oracle table side-car: {Id}", table.Id);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load authored oracle table side-car {File}", file);
                warnings++;
            }
        }

        _logger?.LogInformation("Loaded {Count} authored oracle table side-cars ({Warnings} warnings)", loaded, warnings);
    }

    // <root>/<area-id>/[<kind>/]<file>.yaml -> "<area-id>"
    private string AreaIdFromAuthoringPath(string file)
    {
        var rel = System.IO.Path.GetRelativePath(_root, file).Replace('\\', '/');
        var parts = rel.Split('/');
        return parts.Length > 0 ? parts[0] : "area";
    }
}
