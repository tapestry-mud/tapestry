namespace Tapestry.Shared;

public class CommandContext
{
    public required Guid PlayerEntityId { get; init; }
    public required string RawInput { get; init; }
    public required string Command { get; init; }
    public required string[] Args { get; init; }
}
