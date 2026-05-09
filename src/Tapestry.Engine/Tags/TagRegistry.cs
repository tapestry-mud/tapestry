using System.Text.RegularExpressions;

namespace Tapestry.Engine.Tags;

public class TagRegistry
{
    private readonly Dictionary<string, TagRegistryEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterEngineTag(string name, string description, IEnumerable<string> appliesTo)
    {
        ValidateSnakeCase(name);
        var key = name.ToLowerInvariant();
        if (_entries.ContainsKey(key))
        {
            throw new InvalidOperationException($"Engine tag '{name}' is already registered.");
        }
        _entries[key] = new TagRegistryEntry(
            name,
            "engine",
            description,
            new HashSet<string>(appliesTo, StringComparer.OrdinalIgnoreCase));
    }

    public void RegisterPackTag(string packName, string name, string description, IEnumerable<string> appliesTo)
    {
        ValidateSnakeCase(name);
        if (_entries.TryGetValue(name.ToLowerInvariant(), out var existing) && existing.IsEngineTag)
        {
            throw new InvalidOperationException($"Pack '{packName}' cannot redefine engine tag '{name}'.");
        }
        var fullKey = $"{packName}:{name}".ToLowerInvariant();
        _entries[fullKey] = new TagRegistryEntry(
            name,
            packName,
            description,
            new HashSet<string>(appliesTo, StringComparer.OrdinalIgnoreCase));
    }

    public bool TryResolve(string tag, string? currentPack, out TagRegistryEntry? entry)
    {
        if (_entries.TryGetValue(tag, out entry))
        {
            return true;
        }
        if (currentPack != null && !tag.Contains(':'))
        {
            var prefixed = $"{currentPack}:{tag}".ToLowerInvariant();
            if (_entries.TryGetValue(prefixed, out entry))
            {
                return true;
            }
        }
        entry = null;
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
        if (!Regex.IsMatch(name, @"^[a-z][a-z0-9_]*$"))
        {
            throw new ArgumentException(
                $"Tag name '{name}' must be snake_case (lowercase letters, digits, underscores only).",
                nameof(name));
        }
    }
}
