namespace Tapestry.Engine;

public class DoorState
{
    public string Name { get; set; } = "door";

    public List<string> Keywords =>
        Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

    public bool IsClosed { get; set; }
    public bool IsLocked { get; set; }
    public string? KeyId { get; set; }
    public bool IsPickable { get; set; }
    public int PickDifficulty { get; set; }

    public bool DefaultClosed { get; init; }
    public bool DefaultLocked { get; init; }
}
