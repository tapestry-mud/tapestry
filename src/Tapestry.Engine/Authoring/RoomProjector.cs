// src/Tapestry.Engine/Authoring/RoomProjector.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Shared;

namespace Tapestry.Engine.Authoring;

public sealed class RoomProjector
{
    private readonly World _world;
    private readonly PropertyRegistry _properties;
    private readonly TagRegistry _tags;
    private readonly AreaRegistry _areas;
    private readonly SpawnManager? _spawnManager;

    public RoomProjector(World world, PropertyRegistry properties, TagRegistry tags, AreaRegistry areas,
                         SpawnManager? spawnManager = null)
    {
        _world = world;
        _properties = properties;
        _tags = tags;
        _areas = areas;
        _spawnManager = spawnManager;
    }

    public RoomData Project(Room room)
    {
        var biomeTagNames = _tags.GetAll()
            .Where(t => string.Equals(t.Kind, "biome", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var roomPropNames = _properties.GetAll()
            .Where(p => p.AppliesToType(EntityTypes.Room))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var data = new RoomData
        {
            Id = room.Id,
            Area = room.Area,
            Name = room.Name,
            Description = room.Description,
            Biome = room.Tags.FirstOrDefault(t => biomeTagNames.Contains(t)),
            Tags = room.Tags.Where(t => !biomeTagNames.Contains(t)).OrderBy(t => t).ToList(),
        };

        foreach (var key in roomPropNames)
        {
            var raw = room.GetRawProperty(key);
            if (raw != null)
            {
                data.Properties[key] = raw;
            }
        }

        foreach (var dir in room.AvailableExits())
        {
            var exit = room.GetExit(dir);
            if (exit == null)
            {
                continue;
            }
            var dirKey = dir.ToString().ToLowerInvariant();
            if (exit.IsStub)
            {
                data.Exits[dirKey] = new ExitData { Stub = true, Label = exit.DisplayName };
            }
            else
            {
                data.Exits[dirKey] = new ExitData { Target = exit.TargetRoomId };
            }

            var neighbor = _world.GetRoom(exit.TargetRoomId);
            if (neighbor != null)
            {
                data.Neighbors.Add(new RoomNeighbor
                {
                    Direction = dirKey,
                    Id = neighbor.Id,
                    Name = neighbor.Name,
                    Biome = neighbor.Tags.FirstOrDefault(t => biomeTagNames.Contains(t)),
                    Description = neighbor.Description,
                });
            }
        }

        if (_spawnManager != null && !string.IsNullOrEmpty(room.Area))
        {
            foreach (var rule in _spawnManager.GetRoomSpawns(room.Area!, room.Id))
            {
                var spawnData = new RoomSpawnData { Base = rule.Mob };
                if (rule.Override != null)
                {
                    spawnData.Override = new RoomSpawnOverrideData
                    {
                        FromType = rule.Override.FromType,
                        Name = rule.Override.Name,
                        Desc = rule.Override.Desc,
                        MaxHp = rule.Override.MaxHp,
                        Damage = rule.Override.Damage,
                        Items = rule.Override.Items.ToList()
                    };
                }
                data.Spawns.Add(spawnData);
            }
        }

        if (!string.IsNullOrEmpty(room.Area))
        {
            var areaDef = _areas.Get(room.Area!);
            if (areaDef != null)
            {
                data.AreaName = areaDef.Name;
                data.AreaTheme = string.IsNullOrEmpty(areaDef.Theme) ? null : areaDef.Theme;
            }
        }

        return data;
    }
}
