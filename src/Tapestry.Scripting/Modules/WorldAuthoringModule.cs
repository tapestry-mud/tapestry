using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Recommend;
using Tapestry.Scripting.Connections;
using Tapestry.Shared;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Jint.Native;
using Jint.Runtime;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

/// <summary>
/// JS scripting module (tapestry.authoring.*) for in-MUD world authoring.
/// Every mutating call mutates the live World/Room AND rewrites the room's
/// side-car YAML file under &lt;root&gt;/&lt;area&gt;/rooms/&lt;key&gt;.yaml so the
/// change survives a reboot via the authored-room loader. The serialized YAML
/// round-trips through <see cref="YamlContentLoader.LoadRoom"/>.
/// </summary>
public sealed class WorldAuthoringModule : IJintApiModule
{
    private readonly World _world;
    private readonly RoomProjector _projector;
    private readonly AttributeWriter _writer;
    private readonly string _root;
    private readonly HashSet<string> _loadedPackNamespaces;
    private readonly AreaRegistry _areaRegistry;
    private readonly RecommendBroker? _recommend;
    private readonly ConnectionLoader? _connections;

    // Mirrors ConnectionsModule's serializer, plus OmitEmptyCollections so the
    // recommend-only Neighbors list (cleared before serialization) and any other
    // empty collection (tags/properties/exits) never emit a key. This keeps the
    // side-car a clean pack-room YAML and guarantees no `neighbors:` leaks.
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(
            DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
        .Build();

    public WorldAuthoringModule(
        World world,
        RoomProjector projector,
        AttributeWriter writer,
        string root,
        HashSet<string> loadedPackNamespaces,
        AreaRegistry areaRegistry,
        RecommendBroker? recommend = null,
        ConnectionLoader? connections = null)
    {
        _world = world;
        _projector = projector;
        _writer = writer;
        _root = root;
        _loadedPackNamespaces = loadedPackNamespaces;
        _areaRegistry = areaRegistry;
        _recommend = recommend;
        _connections = connections;
    }

    public string Namespace => "authoring";

    public object Build(JintEngine jint)
    {
        return new
        {
            createArea = new Func<string, string, bool>((id, name) => CreateArea(id, name)),
            getArea = new Func<string, object>(id => GetArea(id)),
            createRoom = new Func<string, string, string, string, bool>(CreateRoom),
            setRoomName = new Func<string, string, object>((roomId, name) =>
            {
                var result = SetRoomName(roomId, name);
                return new
                {
                    ok = result.Ok,
                    id = result.Id,
                    renamed = result.Renamed,
                    warnings = result.Warnings.ToArray()
                };
            }),
            setRoomDescription = new Func<string, string, bool>(SetRoomDescription),
            setRoomExit = new Func<string, string, string, bool>(SetRoomExit),
            removeRoomExit = new Func<string, string, bool>(RemoveRoomExit),
            setRoomAttribute = new Func<string, string, JsValue, string>(SetRoomAttribute),
            clearRoomAttribute = new Func<string, string, string>(ClearRoomAttribute),
            deleteRoom = new Func<string, bool>(DeleteRoom),
            recommendEnabled = new Func<bool>(() => _recommend?.IsEnabled == true)
        };
    }

    public bool CreateRoom(string areaId, string roomId, string name, string description)
    {
        if (string.IsNullOrWhiteSpace(roomId) || !roomId.Contains(':'))
        {
            return false;
        }

        var ns = roomId.Split(':', 2)[0];
        if (!_loadedPackNamespaces.Contains(ns))
        {
            return false;
        }

        if (_world.GetRoom(roomId) != null)
        {
            return false;
        }

        var room = new Room(roomId, name, description) { Area = areaId };
        _world.AddRoom(room);
        WriteSideCar(room);
        return true;
    }

    public SetRoomNameResult SetRoomName(string roomId, string name)
    {
        var room = _world.GetRoom(roomId);
        if (room == null)
        {
            return new SetRoomNameResult { Ok = false, Id = roomId };
        }

        // 1. Set the name (pre-rekey behavior, kept for every path).
        room.Name = name;

        // 2. Pack-room guard: a pack room's id is pack source — name-only, never re-key.
        if (room.GetRawProperty(CommonProperties.SourcePack) != null)
        {
            WriteSideCar(room);
            return new SetRoomNameResult { Ok = true, Id = room.Id };
        }

        // 3-4. Slug the name; bail to a name-only change when nothing usable survives
        // or the key wouldn't actually change.
        var idx = room.Id.IndexOf(':');
        if (idx < 0)
        {
            // Defensive: authored ids are always namespace:key (CreateRoom enforces it).
            // A namespace-less id can't be re-keyed into one — name-only change.
            WriteSideCar(room);
            return new SetRoomNameResult { Ok = true, Id = room.Id };
        }
        var ns = room.Id[..idx];
        var oldKey = room.Id[(idx + 1)..];

        var slug = RoomSlugger.Slugify(name);
        if (slug == null || slug == oldKey)
        {
            WriteSideCar(room);
            return new SetRoomNameResult { Ok = true, Id = room.Id };
        }

        // Dupe-check across the namespace, but the room's OWN key is never "taken" —
        // otherwise re-saving the same name on a suffixed room (gatehouse-2 + "The
        // Gatehouse") would walk the suffix up (-3, -4, ...) on every save.
        var newKey = RoomSlugger.Disambiguate(
            slug, candidate => candidate != oldKey && _world.GetRoom($"{ns}:{candidate}") != null);
        if (newKey == oldKey)
        {
            WriteSideCar(room);
            return new SetRoomNameResult { Ok = true, Id = room.Id };
        }

        return RekeyAndPersist(room, ns, newKey);
    }

    /// <summary>Steps 5-8 of the rename flow: World rekey, side-car persistence,
    /// connection fixup, edge warnings.</summary>
    private SetRoomNameResult RekeyAndPersist(Room room, string ns, string newKey)
    {
        var oldId = room.Id;
        var newId = $"{ns}:{newKey}";

        // Capture the old side-car path BEFORE the rekey (SideCarPath derives from room.Id).
        var oldSideCarPath = SideCarPath(room);

        // 5. In-memory referential integrity (dictionary, exits, entities).
        var rekey = _world.RekeyRoom(oldId, newId);
        if (!rekey.Ok)
        {
            // Defensive: Disambiguate guarantees the target id is free; only a race lands here.
            // Name is already set; id stays unchanged. (Deliberately untested — unreachable
            // through the single-threaded authoring surface.)
            WriteSideCar(room);
            return new SetRoomNameResult { Ok = false, Id = oldId };
        }

        // 6a. Own side-car: write under the new key, delete the old file.
        // Pass oldId so connection-record matching works: records still reference the
        // pre-rekey id at this point (RetargetRoom runs in step 6c).
        WriteSideCar(room, previousId: oldId);
        if (File.Exists(oldSideCarPath))
        {
            File.Delete(oldSideCarPath);
        }

        // 6b. Same-area neighbors whose exits were retargeted: rewrite their side-cars.
        foreach (var neighborId in rekey.RetargetedRoomIds)
        {
            var neighbor = _world.GetRoom(neighborId);
            if (neighbor != null)
            {
                WriteSideCar(neighbor);
            }
        }

        // 6c-7. Edge triage: link-backed edges are connections (fix record + exit);
        // hardcoded edges become warnings. (Cycle C.)
        var warnings = BuildEdgeWarnings(rekey, oldId, newId);

        return new SetRoomNameResult { Ok = true, Id = newId, Renamed = true, Warnings = warnings };
    }

    /// <summary>Edge triage: connection-backed referencers get their record + in-memory exit
    /// fixed; everything else (hardcoded pack exits, other-area authored exits) is warned about.</summary>
    private List<string> BuildEdgeWarnings(RekeyResult rekey, string oldId, string newId)
    {
        // Connections are the supported cross-boundary mechanism: fix every record that
        // references the old id, and retarget the in-memory exit of the room on the
        // other side of each fixed record.
        var fixedByLink = new HashSet<string>();
        if (_connections != null)
        {
            foreach (var record in _connections.RetargetRoom(oldId, newId))
            {
                // After RetargetRoom the renamed side reads newId; the other side names
                // the referencing room.
                var otherRoomId = record.From.Room == newId ? record.To.Room : record.From.Room;
                _world.GetRoom(otherRoomId)?.RetargetExits(oldId, newId);
                fixedByLink.Add(otherRoomId);
            }
        }

        // Whatever edges remain are hardcoded references we must not touch — name them.
        var warnings = new List<string>();
        foreach (var edge in rekey.EdgeReferences)
        {
            if (fixedByLink.Contains(edge.Id))
            {
                continue;
            }
            var kind = edge.IsPackRoom ? "pack room" : "room in another area";
            // Player-facing string: strict ASCII only (em-dashes mojibake over telnet).
            warnings.Add(
                $"{edge.Id} ({kind}) has an exit to this room - not updated; use 'link' or fix the pack.");
        }
        return warnings;
    }

    public bool SetRoomDescription(string roomId, string description)
    {
        var room = _world.GetRoom(roomId);
        if (room == null)
        {
            return false;
        }

        room.Description = description;
        WriteSideCar(room);
        return true;
    }

    public bool SetRoomExit(string roomId, string direction, string targetRoomId)
    {
        var room = _world.GetRoom(roomId);
        if (room == null)
        {
            return false;
        }

        if (!DirectionExtensions.TryParse(direction ?? "", out var dir))
        {
            return false;
        }

        room.SetExit(dir, new Exit(targetRoomId));
        WriteSideCar(room);
        return true;
    }

    public bool RemoveRoomExit(string roomId, string direction)
    {
        var room = _world.GetRoom(roomId);
        if (room == null)
        {
            return false;
        }

        if (!DirectionExtensions.TryParse(direction ?? "", out var dir))
        {
            return false;
        }

        room.RemoveExit(dir);
        WriteSideCar(room);
        return true;
    }

    public string SetRoomAttribute(string roomId, string attr, JsValue value)
    {
        var room = _world.GetRoom(roomId);
        if (room == null)
        {
            return "Room not found.";
        }

        var raw = value.Type == Types.Undefined || value.Type == Types.Null
            ? ""
            : value.ToString();
        var tokens = (raw ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var result = _writer.Write(room, attr, tokens);
        if (result.Ok)
        {
            WriteSideCar(room);
        }
        return result.Message;
    }

    public string ClearRoomAttribute(string roomId, string attr)
    {
        var room = _world.GetRoom(roomId);
        if (room == null)
        {
            return "Room not found.";
        }

        // v1 simplification: "false" clears a tag (and zeroes a bool property).
        var result = _writer.Write(room, attr, new List<string> { "false" });
        if (result.Ok)
        {
            WriteSideCar(room);
        }
        return result.Message;
    }

    public bool DeleteRoom(string roomId)
    {
        var room = _world.GetRoom(roomId);
        if (room == null)
        {
            return false;
        }

        _world.RemoveRoom(roomId);

        var path = SideCarPath(room);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return true;
    }

    private void WriteSideCar(Room room, string? previousId = null)
    {
        var data = _projector.Project(room);
        // Neighbors is recommend-context only; clear it so it never serializes.
        // (OmitEmptyCollections then drops the now-empty list entirely.)
        data.Neighbors.Clear();

        // Connection-backed exits live in connection records, never in room source.
        // Persisting them here would leak hardcoded cross-pack exits into the side-car
        // (and from there into export-area pack output), breaking pack composition.
        RemoveConnectionBackedExits(room.Id, previousId, data);

        var path = SideCarPath(room);
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(path, Serializer.Serialize(data));
    }

    /// <summary>Strip directional exits that are backed by a connection record for this
    /// room. Keyword exits are never in RoomData; one-way connection sides apply no exit.
    /// <paramref name="previousId"/> is the room's id before a rekey: connection records
    /// may still reference the old id when this is called during a rename flow.</summary>
    private void RemoveConnectionBackedExits(string roomId, string? previousId, RoomData data)
    {
        if (_connections == null)
        {
            return;
        }

        foreach (var record in _connections.Loaded)
        {
            ConnectionSide? side = null;
            if (record.From.Room == roomId || record.From.Room == previousId)
            {
                side = record.From;
            }
            else if (record.To.Room == roomId || record.To.Room == previousId)
            {
                side = record.To;
            }

            if (side == null
                || !string.Equals(side.Type, "direction", StringComparison.OrdinalIgnoreCase)
                || !DirectionExtensions.TryParse(side.Direction ?? "", out var dir))
            {
                continue;
            }

            data.Exits.Remove(dir.ToString().ToLowerInvariant());
        }
    }

    private string SideCarPath(Room room)
    {
        var area = room.Area ?? "";
        var idx = room.Id.IndexOf(':');
        var key = idx >= 0 ? room.Id[(idx + 1)..] : room.Id;
        return Path.Combine(_root, area, "rooms", $"{key}.yaml");
    }

    // ---- Area authoring ----

    private string AreaSideCarPath(string areaId)
    {
        // Defensive: area ids are bare slugs today (no colon), but never let an id char
        // break a path segment on Windows. Loading reads the real id from the YAML body,
        // so the directory name need not equal the id.
        return Path.Combine(_root, SafeSegment(areaId), "area.yaml");
    }

    private static string SafeSegment(string id)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = id.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    private void WriteAreaSideCar(AreaDefinition def)
    {
        var path = AreaSideCarPath(def.Id);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) { Directory.CreateDirectory(dir); }
        File.WriteAllText(path, YamlContentLoader.SerializeAreaDefinition(def));
    }

    private static string SlugToName(string areaId)
    {
        var idPart = areaId.Contains(':') ? areaId[(areaId.IndexOf(':') + 1)..] : areaId;
        var words = idPart.Replace('-', ' ').Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    public bool CreateArea(string areaId, string? name)
    {
        if (string.IsNullOrWhiteSpace(areaId) || _areaRegistry.Contains(areaId))
        {
            return false;
        }
        var def = new AreaDefinition
        {
            Id = areaId,
            Name = string.IsNullOrWhiteSpace(name) ? SlugToName(areaId) : name!
        };
        _areaRegistry.Register(def);
        WriteAreaSideCar(def);
        return true;
    }

    public AreaInfo GetArea(string areaId)
    {
        var def = _areaRegistry.Get(areaId);
        if (def == null)
        {
            return AreaInfo.Missing(areaId);
        }
        var sideCar = File.Exists(AreaSideCarPath(areaId));
        return new AreaInfo(def.Id, def.Name, def.Short, def.Description, def.Theme, def.Lore,
            def.LevelRange, def.ResetInterval, def.SourcePack, sideCar, true);
    }
}

/// <summary>Typed result of <see cref="WorldAuthoringModule.SetRoomName"/> — Build()
/// flattens it to the JS { ok, id, renamed, warnings } object.</summary>
public sealed class SetRoomNameResult
{
    public bool Ok { get; init; }

    /// <summary>The room's final id (the new id when renamed, otherwise unchanged).</summary>
    public string Id { get; init; } = "";

    /// <summary>True when the rename re-keyed the id.</summary>
    public bool Renamed { get; init; }

    /// <summary>One line per out-of-sandbox referencer that was NOT updated.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
