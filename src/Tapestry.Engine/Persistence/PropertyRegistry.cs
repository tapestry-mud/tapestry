using System.Text.RegularExpressions;

namespace Tapestry.Engine.Persistence;

public sealed class PropertyRegistry
{
    private static readonly Regex SnakeCasePattern = new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);
    private readonly Dictionary<string, PropertyRegistryEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterEngineProperty(
        string name,
        string description,
        PropertyValueType valueType,
        IEnumerable<string>? appliesTo = null,
        bool transient = false)
    {
        ValidateSnakeCase(name);
        var key = name.ToLowerInvariant();
        var appliesSet = appliesTo != null
            ? (IReadOnlySet<string>)new HashSet<string>(appliesTo, StringComparer.OrdinalIgnoreCase)
            : null;
        if (!_entries.TryAdd(key, new PropertyRegistryEntry(name, "engine", description, valueType, appliesSet, transient)))
        {
            throw new InvalidOperationException($"Engine property '{name}' is already registered.");
        }
    }

    public void RegisterPackProperty(
        string packName,
        string name,
        string description,
        PropertyValueType valueType,
        IEnumerable<string>? appliesTo = null)
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
        _entries[fullKey] = new PropertyRegistryEntry(name, packName, description, valueType, appliesSet, false);
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
        }
        entry = null!;
        return false;
    }

    public bool IsKnown(string name, string? currentPack = null) => TryResolve(name, currentPack, out _);

    public bool IsTransient(string name) =>
        _entries.TryGetValue(name.ToLowerInvariant(), out var entry) && entry.Transient;

    public PropertyValueType? GetValueType(string name) =>
        _entries.TryGetValue(name.ToLowerInvariant(), out var entry) ? entry.ValueType : null;

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
