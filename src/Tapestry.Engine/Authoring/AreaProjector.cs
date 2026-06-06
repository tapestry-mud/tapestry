// src/Tapestry.Engine/Authoring/AreaProjector.cs
using System;
using System.Linq;

namespace Tapestry.Engine.Authoring;

public sealed class AreaProjector
{
    public const int RoomSampleCap = 8;
    private const int SnippetLength = 120;

    private readonly World _world;
    private readonly RoomProjector _roomProjector;

    public AreaProjector(World world, RoomProjector roomProjector)
    {
        _world = world;
        _roomProjector = roomProjector;
    }

    public AreaData Project(AreaDefinition def)
    {
        var data = new AreaData
        {
            Id = def.Id,
            Name = def.Name,
            Short = def.Short,
            Description = def.Description,
            Theme = def.Theme,
            Lore = def.Lore,
            LevelRange = def.LevelRange
        };

        var members = _world.AllRooms
            .Where(r => string.Equals(r.Area, def.Id, StringComparison.OrdinalIgnoreCase))
            .Take(RoomSampleCap)
            .ToList();

        foreach (var room in members)
        {
            var rd = _roomProjector.Project(room);
            var snippet = rd.Description.Length > SnippetLength
                ? rd.Description[..SnippetLength]
                : rd.Description;
            data.Rooms.Add(new AreaRoomSample
            {
                Name = rd.Name,
                Biome = rd.Biome,
                DescriptionSnippet = snippet
            });
        }

        return data;
    }
}
