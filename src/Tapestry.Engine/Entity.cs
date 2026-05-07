using Tapestry.Engine.Stats;

namespace Tapestry.Engine;

public class Entity
{
    public Guid Id { get; }
    public string Type { get; set; }
    public string Name { get; set; }
    public string? LocationRoomId { get; set; }
    public Entity? Container { get; private set; }

    private readonly Dictionary<string, object?> _properties = new();
    private readonly HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Entity> _contents = new();
    private readonly Dictionary<string, Entity> _equipment = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ITagObserver> _tagObservers = new();

    public IReadOnlyList<Entity> Contents => _contents.AsReadOnly();
    public IReadOnlySet<string> Tags => _tags;
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
        if (_properties.TryGetValue(key, out var value) && value is T typed)
        {
            return typed;
        }
        return default;
    }

    public bool TryGetProperty<T>(string key, out T? value)
    {
        if (_properties.TryGetValue(key, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    public bool HasProperty(string key)
    {
        return _properties.ContainsKey(key);
    }

    public IReadOnlyDictionary<string, object?> GetAllProperties()
    {
        return new Dictionary<string, object?>(_properties);
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
