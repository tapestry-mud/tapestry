// src/Tapestry.Engine/Mapping/AreaMap.cs
namespace Tapestry.Engine.Mapping;

/// <summary>
/// Scope of an area-map projection: the whole area (rooted deterministically) or
/// an N-hop radius from the passed root (used by the player map).
/// </summary>
public sealed class MapScope
{
    public static readonly MapScope WholeArea = new(null);

    public static MapScope Radius(int hops)
    {
        return new MapScope(hops);
    }

    /// <summary>Null = whole area; otherwise the BFS hop bound.</summary>
    public int? MaxHops { get; }

    private MapScope(int? maxHops)
    {
        MaxHops = maxHops;
    }
}

/// <summary>One room positioned on the projected grid. Pure data, no ASCII coupling.</summary>
public sealed record RoomCell(
    string Id,
    string Name,
    int X,
    int Y,
    int Z,
    IReadOnlyList<string> Exits,
    IReadOnlyList<string> Markers,
    bool HasVertical,
    bool Collision);

/// <summary>
/// Pure-data projection of an exit graph. The ASCII renderer is one consumer; a
/// future GMCP Area.Map package is another — keep this model serialization-friendly
/// and free of any rendering concern.
/// </summary>
public sealed record AreaMap(
    string AreaId,
    string RootRoomId,
    IReadOnlyList<RoomCell> Cells,
    IReadOnlyList<string> UnpositionedRoomIds);
