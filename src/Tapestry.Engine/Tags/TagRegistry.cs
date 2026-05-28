using System.Text.RegularExpressions;

namespace Tapestry.Engine.Tags;

public sealed class TagRegistry
{
    private static readonly Regex SnakeCasePattern = new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);
    private readonly Dictionary<string, TagRegistryEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private Func<string, IEnumerable<string>>? _dependencyResolver;

    public void RegisterEngineTag(string name, string description, IEnumerable<string> appliesTo, string? kind = null)
    {
        ValidateSnakeCase(name);
        var key = name.ToLowerInvariant();
        if (!_entries.TryAdd(key, new TagRegistryEntry(
            name,
            "engine",
            description,
            new HashSet<string>(appliesTo, StringComparer.OrdinalIgnoreCase),
            kind)))
        {
            throw new InvalidOperationException($"Engine tag '{name}' is already registered.");
        }
    }

    public void RegisterPackTag(string packName, string name, string description, IEnumerable<string> appliesTo, string? kind = null)
    {
        ValidateSnakeCase(name);
        if (_entries.TryGetValue(name.ToLowerInvariant(), out var existing) && existing.IsEngineTag)
        {
            throw new InvalidOperationException($"Pack '{packName}' cannot redefine engine tag '{name}'.");
        }
        var fullKey = $"{packName}:{name}".ToLowerInvariant();
        if (_entries.ContainsKey(fullKey))
        {
            throw new InvalidOperationException($"Pack tag '{packName}:{name}' is already registered.");
        }
        _entries[fullKey] = new TagRegistryEntry(
            name,
            packName,
            description,
            new HashSet<string>(appliesTo, StringComparer.OrdinalIgnoreCase),
            kind);
    }

    public void SetDependencyResolver(Func<string, IEnumerable<string>> resolver)
    {
        _dependencyResolver = resolver;
    }

    public bool TryResolve(string tag, string? currentPack, out TagRegistryEntry entry)
    {
        if (_entries.TryGetValue(tag, out entry!))
        {
            return true;
        }
        if (currentPack != null && !tag.Contains(':'))
        {
            var prefixed = $"{currentPack}:{tag}".ToLowerInvariant();
            if (_entries.TryGetValue(prefixed, out entry!))
            {
                return true;
            }
            if (_dependencyResolver != null)
            {
                foreach (var dep in _dependencyResolver(currentPack))
                {
                    var depKey = $"{dep}:{tag}".ToLowerInvariant();
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

    public bool IsKnown(string tag, string? currentPack = null) => TryResolve(tag, currentPack, out _);

    public IReadOnlyList<TagRegistryEntry> GetAll() => _entries.Values.ToList();

    private static void ValidateSnakeCase(string name)
    {
        if (name.Contains('-'))
        {
            throw new ArgumentException(
                $"Tag name '{name}' contains hyphens. Use snake_case: '{name.Replace('-', '_')}'.",
                nameof(name));
        }
        if (!SnakeCasePattern.IsMatch(name))
        {
            throw new ArgumentException(
                $"Tag name '{name}' must be snake_case (lowercase letters, digits, underscores only).",
                nameof(name));
        }
    }
}
