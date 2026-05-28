using System.Text.RegularExpressions;

namespace Tapestry.Engine.Persistence;

public sealed class PropertyRegistry
{
    private static readonly Regex SnakeCasePattern = new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);
    private readonly Dictionary<string, PropertyRegistryEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private Func<string, IEnumerable<string>>? _dependencyResolver;

    public void RegisterEngineProperty(
        string name,
        string description,
        PropertyValueType valueType,
        IEnumerable<string>? appliesTo = null,
        bool transient = false,
        double? min = null,
        double? max = null,
        IEnumerable<string>? enumValues = null,
        bool settable = true)
    {
        ValidateSnakeCase(name);
        var key = name.ToLowerInvariant();
        var appliesSet = appliesTo != null
            ? (IReadOnlySet<string>)new HashSet<string>(appliesTo, StringComparer.OrdinalIgnoreCase)
            : null;
        var enumSet = enumValues != null
            ? (IReadOnlySet<string>)new HashSet<string>(enumValues, StringComparer.OrdinalIgnoreCase)
            : null;
        if (!_entries.TryAdd(key, new PropertyRegistryEntry(name, "engine", description, valueType, appliesSet, transient, min, max, enumSet, settable)))
        {
            throw new InvalidOperationException($"Engine property '{name}' is already registered.");
        }
    }

    public void RegisterPackProperty(
        string packName,
        string name,
        string description,
        PropertyValueType valueType,
        IEnumerable<string>? appliesTo = null,
        double? min = null,
        double? max = null,
        IEnumerable<string>? enumValues = null)
    {
        ValidateSnakeCase(name);
        if (_entries.TryGetValue(name.ToLowerInvariant(), out var existing) && existing.IsEngineProperty)
        {
            throw new InvalidOperationException($"Pack '{packName}' cannot shadow engine property '{name}'.");
        }
        var fullKey = $"{packName}:{name}".ToLowerInvariant();
        if (_entries.ContainsKey(fullKey))
        {
            throw new InvalidOperationException($"Pack property '{packName}:{name}' is already registered.");
        }
        var appliesSet = appliesTo != null
            ? (IReadOnlySet<string>)new HashSet<string>(appliesTo, StringComparer.OrdinalIgnoreCase)
            : null;
        var enumSet = enumValues != null
            ? (IReadOnlySet<string>)new HashSet<string>(enumValues, StringComparer.OrdinalIgnoreCase)
            : null;
        _entries[fullKey] = new PropertyRegistryEntry(name, packName, description, valueType, appliesSet, false, min, max, enumSet);
    }

    public void SetDependencyResolver(Func<string, IEnumerable<string>> resolver)
    {
        _dependencyResolver = resolver;
    }

    public bool TryResolve(string name, string? currentPack, out PropertyRegistryEntry entry)
    {
        if (_entries.TryGetValue(name, out entry!))
        {
            return true;
        }
        if (currentPack != null && !name.Contains(':'))
        {
            var prefixed = $"{currentPack}:{name}".ToLowerInvariant();
            if (_entries.TryGetValue(prefixed, out entry!))
            {
                return true;
            }
            if (_dependencyResolver != null)
            {
                foreach (var dep in _dependencyResolver(currentPack))
                {
                    var depKey = $"{dep}:{name}".ToLowerInvariant();
                    if (_entries.TryGetValue(depKey, out entry!))
                    {
                        return true;
                    }
                }
            }
        }
        entry = null!;
        return false;
    }

    public bool IsKnown(string name, string? currentPack = null) => TryResolve(name, currentPack, out _);

    /// <summary>
    /// Resolve a property entry by bare name when no pack context is available (e.g. the
    /// player serializer only sees the property key). Prefers an exact key match (engine
    /// properties and already-qualified keys like "tapestry-tinkers:known_recipes"), then a
    /// UNIQUE pack entry whose Name matches. Returns false if ambiguous (same bare name
    /// declared by two or more packs) or unknown.
    /// </summary>
    public bool TryResolveByName(string name, out PropertyRegistryEntry entry)
    {
        // Exact key match covers engine properties (stored as bare name) and already-qualified
        // pack keys (stored as "{pack}:{name}") — e.g. "tapestry-tinkers:known_recipes".
        if (_entries.TryGetValue(name.ToLowerInvariant(), out entry!))
        {
            return true;
        }

        // Scan for a unique pack entry whose bare Name matches (case-insensitive).
        PropertyRegistryEntry? found = null;
        foreach (var candidate in _entries.Values)
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                if (found != null)
                {
                    // Ambiguous — same bare name registered by two or more packs.
                    entry = null!;
                    return false;
                }
                found = candidate;
            }
        }

        if (found != null)
        {
            entry = found;
            return true;
        }

        entry = null!;
        return false;
    }

    public bool IsTransient(string name) =>
        _entries.TryGetValue(name.ToLowerInvariant(), out var entry) && entry.Transient;

    public PropertyValueType? GetValueType(string name, string? packName = null)
    {
        if (TryResolve(name, packName, out var entry))
        {
            return entry.ValueType;
        }
        return null;
    }

    public IReadOnlyList<PropertyRegistryEntry> GetAll() => _entries.Values.ToList();

    private static void ValidateSnakeCase(string name)
    {
        if (name.Contains('-'))
        {
            throw new ArgumentException(
                $"Property name '{name}' contains hyphens. Use snake_case: '{name.Replace('-', '_')}'.",
                nameof(name));
        }
        if (!SnakeCasePattern.IsMatch(name))
        {
            throw new ArgumentException(
                $"Property name '{name}' must be snake_case (lowercase letters, digits, underscores only).",
                nameof(name));
        }
    }
}
