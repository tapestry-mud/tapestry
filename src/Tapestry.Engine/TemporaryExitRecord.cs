namespace Tapestry.Engine;

public class TemporaryExitRecord
{
    public string Id { get; init; } = "";
    public string RoomId { get; init; } = "";
    public string Keyword { get; init; } = "";
    public string? PairedId { get; init; }
    public int ExpiryTickCount { get; init; }
    public string DisplayName { get; init; } = "portal";
    public string TargetRoomId { get; init; } = "";
}
