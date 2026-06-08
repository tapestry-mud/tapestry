namespace Tapestry.Scripting.Modules;

// Intentionally exposes only the fields the area editor/provenance surface needs for Spec A.
// Weather and occupied fields are omitted by design; Wip is the one flags-derived field
// surfaced (the WIP visibility flag).
public sealed record AreaInfo(
    string Id, string Name, string Short, string Description, string Theme, string Lore,
    int[] LevelRange, int ResetInterval, string? SourcePack, bool SideCar, bool Exists, bool Wip)
{
    public static AreaInfo Missing(string id) =>
        new(id, "", "", "", "", "", new[] { 1, 99 }, 0, null, false, false, false);
}

/// <summary>
/// One-line area summary returned by <see cref="WorldAuthoringModule.GetAreas"/> for the
/// builder <c>areas</c> command. Includes provenance tag, room count, and override count.
/// </summary>
public sealed record AreaSummary(
    string Id, string Name, string Short, int[] LevelRange,
    string Provenance, int RoomCount, int OverrideCount, bool Wip);

/// <summary>
/// Per-room summary returned by <see cref="WorldAuthoringModule.GetAreaRooms"/> for the
/// builder <c>rooms &lt;area&gt;</c> command. Includes provenance tag for each room.
/// </summary>
public sealed record RoomSummary(string Id, string Name, string Provenance);
