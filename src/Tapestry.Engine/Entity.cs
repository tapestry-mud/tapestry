using Tapestry.Engine.Economy;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Stats;
using Tapestry.Engine.Training;

namespace Tapestry.Engine;

public class Entity : IAttributeTarget
{
    public Guid Id { get; }
    public string Type { get; set; }
    public string Name { get; set; }
    public string? LocationRoomId { get; set; }
    public Entity? Container { get; private set; }

    private readonly Dictionary<string, object?> _properties = new();
    private readonly HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _keywords = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Entity> _contents = new();
    private readonly Dictionary<string, Entity> _equipment = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ITagObserver> _tagObservers = new();

    public IReadOnlyList<Entity> Contents => _contents.AsReadOnly();
    public IReadOnlySet<string> Tags => _tags;
    public IReadOnlySet<string> Keywords => _keywords;
    public IReadOnlySet<string> Roles => _roles;
    public Disposition Disposition { get; set; } = Disposition.Neutral;
    public DispositionDefinition? DispositionRules { get; set; }
    public ShopConfig? ShopConfig { get; set; }
    public TrainerConfig? TrainerConfig { get; set; }
    public StatBlock Stats { get; } = new();
    public IReadOnlyDictionary<string, Entity> Equipment => _equipment;

    public Entity(string type, string name, Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        Type = type;
        Name = name;
    }

    public void SetProperty(string key, object? value)
    {
        if (value == null)
        {
            _properties.Remove(key);
        }
        else
        {
            _properties[key] = value;
        }
    }

    public T? GetProperty<T>(string key)
    {
        if (_properties.TryGetValue(key, out var value))
        {
            if (value is T typed)
            {
                return typed;
            }

            if (typeof(T) == typeof(List<string>) && value is List<object> objList)
            {
                return (T)(object)objList.OfType<string>().ToList();
            }

            if (TryCoerceNumeric<T>(value, out var coerced))
            {
                return coerced;
            }
        }
        return default;
    }

    public bool TryGetProperty<T>(string key, out T? value)
    {
        if (_properties.TryGetValue(key, out var raw))
        {
            if (raw is T typed)
            {
                value = typed;
                return true;
            }

            if (TryCoerceNumeric<T>(raw, out value))
            {
                return true;
            }
        }
        value = default;
        return false;
    }

    // JS (Jint) stores numbers as double; C# callers often want int. Coerce between
    // numeric types so Get/TryGetProperty read reliably regardless of which side wrote
    // the value. bool, char, and string are intentionally never coerced.
    private static bool TryCoerceNumeric<T>(object? raw, out T? value)
    {
        value = default;
        if (raw == null || !IsNumeric(raw) || !IsNumericType(typeof(T)))
        {
            return false;
        }
        try
        {
            value = (T)Convert.ChangeType(raw, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
            return true;
        }
        catch (OverflowException) { return false; }
        catch (InvalidCastException) { return false; }
        catch (FormatException) { return false; }
    }

    private static bool IsNumeric(object value)
    {
        return value is int or long or short or sbyte or byte or uint or ulong or ushort
            or double or float or decimal;
    }

    private static bool IsNumericType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(int) || type == typeof(long) || type == typeof(short)
            || type == typeof(sbyte) || type == typeof(byte) || type == typeof(uint)
            || type == typeof(ulong) || type == typeof(ushort) || type == typeof(double)
            || type == typeof(float) || type == typeof(decimal);
    }

    public bool HasProperty(string key)
    {
        return _properties.ContainsKey(key);
    }

    public void SetMapValue<T>(string property, string key, T value)
    {
        if (!_properties.TryGetValue(property, out var raw) || raw is not Dictionary<string, T> map)
        {
            map = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            _properties[property] = map;
        }
        map[key] = value;
    }

    public T? GetMapValue<T>(string property, string key)
    {
        if (_properties.TryGetValue(property, out var raw) && raw is Dictionary<string, T> map
            && map.TryGetValue(key, out var value))
        {
            return value;
        }
        return default;
    }

    public IReadOnlyDictionary<string, T>? GetMap<T>(string property)
    {
        if (_properties.TryGetValue(property, out var raw) && raw is Dictionary<string, T> map)
        {
            return map;
        }
        return null;
    }

    public void RemoveMapKey(string property, string key)
    {
        RemoveFromTypedMap<int>(property, key);
        RemoveFromTypedMap<string>(property, key);
        RemoveFromTypedMap<double>(property, key);
        RemoveFromTypedMap<bool>(property, key);
        RemoveFromTypedMap<long>(property, key);
    }

    private void RemoveFromTypedMap<T>(string property, string key)
    {
        if (_properties.TryGetValue(property, out var raw) && raw is Dictionary<string, T> map)
        {
            map.Remove(key);
        }
    }

    public IReadOnlyDictionary<string, object?> GetAllProperties()
    {
        return new Dictionary<string, object?>(_properties);
    }

    public IEnumerable<string> GetAllPropertyKeys() => _properties.Keys;
    public object? GetRawProperty(string key) => _properties.GetValueOrDefault(key);

    public IEnumerable<KeyValuePair<string, object?>> EnumerateProperties(string prefix)
    {
        foreach (var kv in _properties)
        {
            if (kv.Key.StartsWith(prefix))
            {
                yield return kv;
            }
        }
    }

    public void AddTag(string tag)
    {
        if (_tags.Add(tag))
        {
            foreach (var obs in _tagObservers)
            {
                obs.OnTagAdded(this, tag);
            }
        }
    }

    public void RemoveTag(string tag)
    {
        if (_tags.Remove(tag))
        {
            foreach (var obs in _tagObservers)
            {
                obs.OnTagRemoved(this, tag);
            }
        }
    }

    public bool HasTag(string tag)
    {
        return _tags.Contains(tag);
    }

    public void AddKeyword(string keyword)
    {
        _keywords.Add(keyword);
    }

    public bool HasKeyword(string keyword)
    {
        return _keywords.Contains(keyword);
    }

    public void AddRole(string role)
    {
        _roles.Add(role);
    }

    public bool HasRole(string role)
    {
        return _roles.Contains(role);
    }

    public void RemoveRole(string role)
    {
        _roles.Remove(role);
    }

    public void RegisterTagObserver(ITagObserver observer)
    {
        _tagObservers.Add(observer);
    }

    public void UnregisterTagObserver(ITagObserver observer)
    {
        _tagObservers.Remove(observer);
    }

    public void AddToContents(Entity entity)
    {
        entity.Container?.RemoveFromContents(entity);
        entity.LocationRoomId = null;
        entity.Container = this;
        _contents.Add(entity);
    }

    public void RemoveFromContents(Entity entity)
    {
        if (_contents.Remove(entity))
        {
            entity.Container = null;
        }
    }

    public void SetEquipment(string slot, Entity item)
    {
        _equipment[slot] = item;
    }

    public void RemoveEquipment(string slot)
    {
        _equipment.Remove(slot);
    }

    public Entity? GetEquipment(string slot)
    {
        return _equipment.GetValueOrDefault(slot);
    }
}
