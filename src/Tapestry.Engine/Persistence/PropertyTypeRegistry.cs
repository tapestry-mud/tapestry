namespace Tapestry.Engine.Persistence;

public class PropertyTypeRegistry
{
    private readonly Dictionary<string, Type> _types = new();
    private readonly Dictionary<string, Type> _prefixes = new();
    private readonly HashSet<string> _transient = new();

    public void Register(string key, Type type, bool transient = false)
    {
        _types[key] = type;
        if (transient) { _transient.Add(key); }
    }

    public void RegisterPrefix(string prefix, Type type)
    {
        _prefixes[prefix] = type;
    }

    public void RegisterTransient(string key)
    {
        _transient.Add(key);
    }

    public Type? GetType(string key)
    {
        if (_types.TryGetValue(key, out var type))
        {
            return type;
        }
        foreach (var kvp in _prefixes)
        {
            if (key.StartsWith(kvp.Key))
            {
                return kvp.Value;
            }
        }
        return null;
    }

    public bool IsRegistered(string key)
    {
        return GetType(key) != null;
    }

    public bool IsTransient(string key)
    {
        return _transient.Contains(key);
    }
}
