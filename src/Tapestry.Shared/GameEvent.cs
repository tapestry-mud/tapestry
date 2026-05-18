namespace Tapestry.Shared;

public class GameEvent : IRoomEvent
{
    public required string Type { get; init; }
    public Guid? SourceEntityId { get; init; }
    public Guid? TargetEntityId { get; init; }
    public string? RoomId { get; init; }
    public string? SourceEntityName { get; init; }
    private Dictionary<string, object?>? _data;
    public Dictionary<string, object?> Data
    {
        get => _data ??= new();
        init => _data = value;
    }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public bool Cancelled { get; set; }
}
