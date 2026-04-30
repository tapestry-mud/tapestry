using Tapestry.Engine.Alignment;
using Tapestry.Shared;

namespace Tapestry.Engine;

public class Room
{
    public string Id { get; }
    public string Name { get; set; }
    public string Description { get; set; }

    private readonly Dictionary<Direction, Exit> _exits = new();
    private readonly Dictionary<string, Exit> _keywordExits =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Entity> _entities = new();
    private readonly HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _properties = new();

    public AlignmentRange? AlignmentRange { get; set; }
    public string? AlignmentBlockMessage { get; set; }
    public string? Area { get; set; }
    public bool WeatherExposed { get; set; }
    public bool TimeExposed { get; set; }
    public Dictionary<string, WeatherMessages> WeatherMessages { get; set; } = new();
    public Dictionary<string, string> TimeMessages { get; set; } = new();

    public IReadOnlyList<Entity> Entities => _entities.AsReadOnly();
    public IReadOnlySet<string> Tags => _tags;
    public IReadOnlyDictionary<string, Exit> KeywordExits => _keywordExits;

    public Room(string id, string name, string description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    public void SetExit(Direction direction, Exit exit)
    {
        _exits[direction] = exit;
    }

    public Exit? GetExit(Direction direction)
    {
        return _exits.GetValueOrDefault(direction);
    }

    public void RemoveExit(Direction direction)
    {
        _exits.Remove(direction);
    }

    public IEnumerable<Direction> AvailableExits()
    {
        return _exits.Keys;
    }

    public void SetKeywordExit(string keyword, Exit exit)
    {
        _keywordExits[keyword] = exit;
    }

    public Exit? GetKeywordExit(string keyword)
    {
        return _keywordExits.TryGetValue(keyword, out var exit) ? exit : null;
    }

    public void RemoveKeywordExit(string keyword)
    {
        _keywordExits.Remove(keyword);
    }

    public bool HasKeywordExit(string keyword)
    {
        return _keywordExits.ContainsKey(keyword);
    }

    public void AddEntity(Entity entity)
    {
        entity.Container?.RemoveFromContents(entity);
        entity.LocationRoomId = Id;
        _entities.Add(entity);
    }

    public void RemoveEntity(Entity entity)
    {
        if (_entities.Remove(entity))
        {
            entity.LocationRoomId = null;
        }
    }

    public void AddTag(string tag)
    {
        _tags.Add(tag);
    }

    public bool HasTag(string tag)
    {
        return _tags.Contains(tag);
    }

    public void SetProperty(string key, object? value)
    {
        _properties[key] = value;
    }

    public T? GetProperty<T>(string key)
    {
        if (_properties.TryGetValue(key, out var value) && value is T typed)
        {
            return typed;
        }
        return default;
    }
}

public record WeatherMessages(string? Start, string? Ongoing, string? End);
