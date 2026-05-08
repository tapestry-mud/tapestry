namespace Tapestry.Engine;

public class ActorContext
{
    public Guid EntityId { get; init; }
    public string Name { get; init; } = "";
    public string? RoomId { get; init; }
    public string Source { get; init; } = "player";   // "player" | "mob"
    public string RawInput { get; init; } = "";
    public string Command { get; init; } = "";
    public string[] RawArgs { get; init; } = [];
}
