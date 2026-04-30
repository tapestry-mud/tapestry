namespace Tapestry.Engine;

public class Exit
{
    public string TargetRoomId { get; set; }
    public Dictionary<string, object?> Conditions { get; set; } = new();
    public DoorState? Door { get; set; }
    public string? DisplayName { get; set; }

    public Exit(string targetRoomId)
    {
        TargetRoomId = targetRoomId;
    }
}
