// src/Tapestry.Engine/Mapping/AreaMapProjector.cs
using Tapestry.Engine.Tags;
using Tapestry.Shared;

namespace Tapestry.Engine.Mapping;

/// <summary>
/// Projects a room's exit graph into relative 2D/3D cells via BFS. Coordinates are
/// DERIVED from exit deltas — there is no stored grid, so non-Euclidean layouts stay
/// legal (they flag <see cref="RoomCell.Collision"/> instead of failing).
/// Reads only the World/Room graph; never mutates.
/// </summary>
public sealed class AreaMapProjector
{
    private readonly World _world;
    private readonly TagRegistry _tags;

    private static readonly Dictionary<Direction, (int Dx, int Dy, int Dz)> Deltas = new()
    {
        [Direction.North] = (0, 1, 0),
        [Direction.South] = (0, -1, 0),
        [Direction.East] = (1, 0, 0),
        [Direction.West] = (-1, 0, 0),
        [Direction.Up] = (0, 0, 1),
        [Direction.Down] = (0, 0, -1),
    };

    public AreaMapProjector(World world, TagRegistry tags)
    {
        _world = world;
        _tags = tags;
    }

    public AreaMap Project(Room root, MapScope scope)
    {
        var start = scope.MaxHops == null ? FindDeterministicRoot(root) : root;
        var biomeNames = CollectBiomeTagNames();

        var positions = new Dictionary<string, (int X, int Y, int Z)>();
        var depths = new Dictionary<string, int>();
        var occupied = new Dictionary<(int X, int Y, int Z), string>();
        var collided = new HashSet<string>();
        var queue = new Queue<Room>();

        positions[start.Id] = (0, 0, 0);
        depths[start.Id] = 0;
        occupied[(0, 0, 0)] = start.Id;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var room = queue.Dequeue();
            var depth = depths[room.Id];
            if (scope.MaxHops != null && depth >= scope.MaxHops.Value)
            {
                continue;
            }
            var pos = positions[room.Id];

            // Enum order keeps the BFS deterministic regardless of exit insertion order.
            foreach (var dir in room.AvailableExits().OrderBy(d => (int)d))
            {
                var exit = room.GetExit(dir);
                if (exit == null)
                {
                    continue;
                }
                var target = _world.GetRoom(exit.TargetRoomId);
                if (target == null)
                {
                    continue;
                }
                if (scope.MaxHops == null && !SameAreaAs(start, target))
                {
                    continue;
                }
                if (positions.ContainsKey(target.Id))
                {
                    continue; // a room first reached wins its cell
                }

                var (dx, dy, dz) = Deltas[dir];
                var cell = (pos.X + dx, pos.Y + dy, pos.Z + dz);
                if (occupied.TryGetValue(cell, out var holder))
                {
                    // Honest non-Euclidean failure: both rooms flag collision.
                    collided.Add(holder);
                    collided.Add(target.Id);
                }
                else
                {
                    occupied[cell] = target.Id;
                }
                positions[target.Id] = cell;
                depths[target.Id] = depth + 1;
                queue.Enqueue(target);
            }
        }

        var cells = positions
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p =>
            {
                var room = _world.GetRoom(p.Key);
                return room == null
                    ? null
                    : BuildCell(room, p.Value, collided.Contains(p.Key), biomeNames);
            })
            .Where(c => c != null)
            .Select(c => c!)
            .ToList();

        var unpositioned = new List<string>();
        if (scope.MaxHops == null)
        {
            unpositioned = _world.AllRooms
                .Where(r => SameAreaAs(start, r) && !positions.ContainsKey(r.Id))
                .Select(r => r.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }

        return new AreaMap(start.Area ?? "", start.Id, cells, unpositioned);
    }

    /// <summary>Whole-area projections root at the lexicographically lowest room id in
    /// the area so the layout is identical regardless of where the caller stands.</summary>
    private Room FindDeterministicRoot(Room root)
    {
        return _world.AllRooms
            .Where(r => SameAreaAs(root, r))
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .FirstOrDefault() ?? root;
    }

    private static bool SameAreaAs(Room a, Room b)
    {
        return string.Equals(a.Area, b.Area, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Room tags are stored bare ("forest") but pack tags register under scoped
    /// keys ("tapestry-biomes:forest") — match Name AND FullName, same as
    /// WorldModule.getRoomBiome.</summary>
    private HashSet<string> CollectBiomeTagNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _tags.GetAll())
        {
            if (string.Equals(entry.Kind, "biome", StringComparison.OrdinalIgnoreCase))
            {
                names.Add(entry.Name);
                names.Add(entry.FullName);
            }
        }
        return names;
    }

    private static RoomCell BuildCell(
        Room room,
        (int X, int Y, int Z) pos,
        bool collision,
        HashSet<string> biomeNames)
    {
        var exitDirs = room.AvailableExits().ToList();

        var exits = exitDirs
            .Select(d => d.ToString().ToLowerInvariant())
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();

        var hasVertical = exitDirs.Any(d => d == Direction.Up || d == Direction.Down);

        var markers = new List<string>();
        var terrain = room.GetProperty<string>("terrain");
        if (!string.IsNullOrWhiteSpace(terrain))
        {
            markers.Add(terrain.ToLowerInvariant());
        }
        var biome = room.Tags.FirstOrDefault(t => biomeNames.Contains(t));
        if (biome != null && !markers.Contains(biome.ToLowerInvariant()))
        {
            markers.Add(biome.ToLowerInvariant());
        }

        return new RoomCell(
            room.Id, room.Name, pos.X, pos.Y, pos.Z,
            exits, markers, hasVertical, collision);
    }
}
