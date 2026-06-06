using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Tapestry.Engine;

namespace Tapestry.Scripting.Authoring;

/// <summary>
/// Always runs at boot (even without the builder pack). Scans &lt;root&gt;/&lt;area&gt;/area.yaml
/// side-cars and upserts them into the AreaRegistry. A side-car whose id matches a packed
/// area overlays its fields while preserving the packed area's SourcePack (-> "[pack +edits]").
/// An authored-only side-car carries no SourcePack (-> "[authored]").
/// </summary>
public class AuthoredAreaLoader
{
    private readonly string _root;
    private readonly AreaRegistry _registry;
    private readonly ILogger<AuthoredAreaLoader> _logger;

    public AuthoredAreaLoader(string root, AreaRegistry registry, ILogger<AuthoredAreaLoader> logger)
    {
        _root = root;
        _registry = registry;
        _logger = logger;
    }

    public void Load()
    {
        if (!Directory.Exists(_root))
        {
            _logger.LogDebug("Authored areas root {Root} missing; nothing to load.", _root);
            return;
        }

        var files = Directory.GetFiles(_root, "area.yaml", SearchOption.AllDirectories)
            .OrderBy(f => f)
            .ToList();

        var loaded = 0;
        var warnings = 0;

        foreach (var file in files)
        {
            try
            {
                var yaml = File.ReadAllText(file);
                var def = YamlContentLoader.LoadAreaDefinition(yaml);
                var existing = _registry.Get(def.Id);
                if (existing != null && !string.IsNullOrEmpty(existing.SourcePack))
                {
                    def.SourcePack = existing.SourcePack;
                }
                _registry.Register(def);
                loaded++;
                _logger.LogDebug("  Authored area side-car: {Id}", def.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load authored area side-car {File}", file);
                warnings++;
            }
        }

        _logger.LogInformation("Loaded {Count} authored area side-cars ({Warnings} warnings)", loaded, warnings);
    }
}
