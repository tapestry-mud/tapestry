namespace Tapestry.Engine.Authoring;

/// <summary>Serializable exit projection: a real exit carries Target (emitted as a bare
/// scalar, byte-identical to the legacy form); a stub carries Stub=true + Label and no
/// target (emitted as a mapping). Replaces the bare string exit value in RoomData.</summary>
public sealed class ExitData
{
    public string? Target { get; set; }
    public bool Stub { get; set; }
    public string? Label { get; set; }
}
