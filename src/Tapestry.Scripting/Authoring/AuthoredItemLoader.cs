using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Tapestry.Engine.Items;
using Tapestry.Engine.Persistence;

namespace Tapestry.Scripting.Authoring;

/// <summary>
/// Boot-time scanner that re-registers frozen item-template side-cars from the authoring root.
/// Mirrors <see cref="AuthoredOracleLoader"/>: scans <c>&lt;root&gt;/**/items/*.yaml</c>,
/// loads each via <see cref="YamlContentLoader.LoadItem"/> (WITH the PropertyRegistry so the
/// nested ac map coerces to Dictionary&lt;string,int&gt;), maps to an ItemTemplate (identical
/// to PackLoader.LoadItems), and registers it. Reload half of the runtime writeItemTemplate freeze.
/// </summary>
public class AuthoredItemLoader
{
    private readonly string _root;
    private readonly ItemRegistry _registry;
    private readonly PropertyRegistry _properties;
    private readonly ILogger<AuthoredItemLoader>? _logger;

    // PropertyRegistry is REQUIRED (BLOCKER 1): without it LoadItem leaves the ac map as a raw
    // object map and worn armor reads 0 AC. Mirror PackLoader, which always passes _propertyRegistry.
    public AuthoredItemLoader(string root, ItemRegistry registry, PropertyRegistry properties,
        ILogger<AuthoredItemLoader>? logger = null)
    {
        _root = root;
        _registry = registry;
        _properties = properties;
        _logger = logger;
    }

    public void Load()
    {
        if (!Directory.Exists(_root))
        {
            _logger?.LogDebug("Authored items root {Root} missing; nothing to load.", _root);
            return;
        }

        var files = Directory.EnumerateFiles(_root, "*.yaml", SearchOption.AllDirectories)
            .Where(f =>
            {
                var dir = Path.GetFileName(Path.GetDirectoryName(f) ?? "");
                return string.Equals(dir, "items", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(f => f)
            .ToList();

        var loaded = 0;
        var warnings = 0;
        foreach (var file in files)
        {
            try
            {
                // Pass the PropertyRegistry so `ac` resolves to Dictionary<string,int> (MapInt).
                var def = YamlContentLoader.LoadItem(File.ReadAllText(file), _properties);
                var template = new ItemTemplate
                {
                    Id = def.Id,
                    Name = def.Name,
                    Type = def.Type,
                    Tags = new List<string>(def.Tags),
                    Keywords = new List<string>(def.Keywords),
                    Properties = def.Properties.ToDictionary(kv => kv.Key, kv => (object?)kv.Value),
                };
                _registry.Register(template);
                loaded++;
                _logger?.LogDebug("  Authored item side-car: {Id}", template.Id);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load authored item side-car {File}", file);
                warnings++;
            }
        }
        _logger?.LogInformation("Loaded {Count} authored item side-cars ({Warnings} warnings)", loaded, warnings);
    }
}
