// src/Tapestry.Engine/Authoring/RoomData.cs
using System.Collections.Generic;
using Tapestry.Engine.Recommend;

namespace Tapestry.Engine.Authoring;

/// <summary>Plain-object projection of a <see cref="Room"/> — the single
/// serialization/recommendation/map source. Field order/casing matches the
/// pack room YAML schema so export is a near file-move.</summary>
public sealed class RoomData : IRecommendContext
{
    public string Id { get; set; } = "";
    public string? Area { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Single biome tag name (registry kind == "biome"), or null.</summary>
    public string? Biome { get; set; }
    /// <summary>Other room tags (excludes the biome tag).</summary>
    public List<string> Tags { get; set; } = new();
    /// <summary>Declared room properties (applies_to:[room]) present on the room.</summary>
    public Dictionary<string, object?> Properties { get; set; } = new();
    /// <summary>Direction (lowercased) → target room id.</summary>
    public Dictionary<string, string> Exits { get; set; } = new();
    /// <summary>1-hop neighbor summaries (recommend context only; not serialized to the side-car).</summary>
    public List<RoomNeighbor> Neighbors { get; set; } = new();
}

public sealed class RoomNeighbor
{
    public string Direction { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Biome { get; set; }
    /// <summary>The neighbor's description (recommend context only) — lets adjacency propagate theme.</summary>
    public string? Description { get; set; }
}
