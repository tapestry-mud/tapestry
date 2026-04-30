namespace Tapestry.Shared;

public class GameEvent : IRoomEvent
{
    public required string Type { get; init; }
    public Guid? SourceEntityId { get; init; }
    public Guid? TargetEntityId { get; init; }
    public string? RoomId { get; init; }
    public string? SourceEntityName { get; init; }
    public Dictionary<string, object?> Data { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public bool Cancelled { get; set; }
}
