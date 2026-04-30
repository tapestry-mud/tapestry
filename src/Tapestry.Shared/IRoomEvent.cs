namespace Tapestry.Shared;

/// <summary>
/// Marker interface for events that occur in a room and may be observed by nearby entities.
/// </summary>
public interface IRoomEvent
{
    string? RoomId { get; }
    Guid? SourceEntityId { get; }
    string? SourceEntityName { get; }
}
