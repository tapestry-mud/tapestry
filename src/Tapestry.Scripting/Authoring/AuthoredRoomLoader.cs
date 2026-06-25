using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;

namespace Tapestry.Scripting.Authoring;

/// <summary>Boot re-apply of the rooms side-car (game-root data/areas/&lt;area&gt;/rooms/*.yaml).
/// Mirrors <c>ConnectionLoader</c>: warn-and-skip on bad files / unresolved targets;
/// always present so authored areas load even without the @tapestry/builder pack.</summary>
public class AuthoredRoomLoader
{
    private readonly World _world;
    private readonly ILogger<AuthoredRoomLoader> _logger;
    private readonly string _roomsRoot;
    private readonly PropertyRegistry _properties;
    private readonly TagRegistry _tags;

    public AuthoredRoomLoader(
        World world,
        ILogger<AuthoredRoomLoader> logger,
        string roomsRoot,
        PropertyRegistry properties,
        TagRegistry tags)
    {
        _world = world;
        _logger = logger;
        _roomsRoot = roomsRoot;
        _properties = properties;
        _tags = tags;
    }

    public void Load()
    {
        if (!Directory.Exists(_roomsRoot))
        {
            _logger.LogInformation("No authored rooms directory at {Path}, skipping", _roomsRoot);
            return;
        }

        var files = Directory.GetFiles(_roomsRoot, "*.yaml", SearchOption.AllDirectories)
            // Only load files whose immediate parent directory is named "rooms".
            // Item side-cars live under items/, oracle tables under mobs/boss/prose/places/,
            // and area.yaml lives at the area root -- all are excluded by this filter.
            .Where(f => string.Equals(
                Path.GetFileName(Path.GetDirectoryName(f)), "rooms",
                StringComparison.OrdinalIgnoreCase))
            // Oracle table files end in -oracle-table.yaml -- skip them (not real rooms).
            .Where(f => !Path.GetFileName(f).EndsWith("-oracle-table.yaml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToList();

        var loaded = 0;
        var warnings = 0;
        var applied = new List<Room>();

        foreach (var file in files)
        {
            try
            {
                var yaml = File.ReadAllText(file);
                var result = YamlContentLoader.LoadRoom(yaml, _properties, _tags);
                var room = result.Room;
                if (_world.GetRoom(room.Id) != null)
                {
                    _logger.LogWarning("Authored room {Id} ({File}) already exists, skipping", room.Id, file);
                    warnings++;
                    continue;
                }
                _world.AddRoom(room);
                applied.Add(room);
                loaded++;
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning("Failed to load authored room {File}: {Error}", file, ex.Message);
                warnings++;
            }
        }

        foreach (var room in applied)
        {
            foreach (var dir in room.AvailableExits())
            {
                var exit = room.GetExit(dir);
                if (exit != null && !exit.IsStub && _world.GetRoom(exit.TargetRoomId) == null)
                {
                    _logger.LogWarning("Authored room {Id} exit {Dir} -> '{Target}' has no target room",
                        room.Id, dir, exit.TargetRoomId);
                    warnings++;
                }
            }
        }

        _logger.LogInformation("Loaded {Count} authored rooms ({Warnings} warnings)", loaded, warnings);
    }
}
