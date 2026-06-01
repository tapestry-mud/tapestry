namespace Tapestry.Engine;

/// <summary>An out-of-sandbox room that references a re-keyed room. Reported, never touched —
/// the authoring layer decides whether it's link-backed (fixable) or hardcoded (warn).</summary>
public sealed record RoomRef(string Id, string Name, bool IsPackRoom);

/// <summary>Result of <see cref="World.RekeyRoom"/>.</summary>
public sealed class RekeyResult
{
    public bool Ok { get; init; }

    /// <summary>Same-area rooms whose exits were retargeted to the new id.</summary>
    public IReadOnlyList<string> RetargetedRoomIds { get; init; } = [];

    /// <summary>Out-of-sandbox referencers (pack rooms / other areas) — NOT touched.</summary>
    public IReadOnlyList<RoomRef> EdgeReferences { get; init; } = [];

    public static readonly RekeyResult Failed = new() { Ok = false };
}
