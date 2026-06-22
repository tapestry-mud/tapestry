using System.Collections.Generic;
using Tapestry.Engine.Authoring;

namespace Tapestry.Engine.Recommend;

/// <summary>An IRecommendContext carrying the engine-projected room context (neighbors,
/// area, biome - via RoomProjector) PLUS the pack-owned template, system voice, and
/// variables. The engine projects the context; the pack owns the wording.</summary>
public sealed class PackRoomContext : IRecommendContext
{
    public RoomData Room { get; set; } = new();
    public string Template { get; set; } = "";
    public string? System { get; set; }
    public IReadOnlyDictionary<string, string> Vars { get; set; } = new Dictionary<string, string>();
}
