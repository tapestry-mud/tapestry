// src/Tapestry.Engine/Authoring/AreaData.cs
using System.Collections.Generic;
using Tapestry.Engine.Recommend;

namespace Tapestry.Engine.Authoring;

public sealed class AreaData : IRecommendContext
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Short { get; set; } = "";
    public string Description { get; set; } = "";
    public string Theme { get; set; } = "";
    public string Lore { get; set; } = "";
    public int[] LevelRange { get; set; } = [1, 99];
    public List<AreaRoomSample> Rooms { get; set; } = new();
}

public sealed class AreaRoomSample
{
    public string Name { get; set; } = "";
    public string? Biome { get; set; }
    public string DescriptionSnippet { get; set; } = "";
}
