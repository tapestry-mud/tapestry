using System.Collections.Generic;
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
        RecommendBroker? recommend = null,
        ConnectionLoader? connections = null)
    {
        _world = world;
        _projector = projector;
        _writer = writer;
        _root = root;
        _loadedPackNamespaces = loadedPackNamespaces;
        _recommend = recommend;
        _connections = connections;
    }

    public string Namespace => "authoring";

    public object Build(JintEngine jint)
    {
        return new
        {
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
        var ns = idx >= 0 ? room.Id[..idx] : "";
        var oldKey = idx >= 0 ? room.Id[(idx + 1)..] : room.Id;

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
        // Cycle B/C implementation lands here.
        WriteSideCar(room);
        return new SetRoomNameResult { Ok = true, Id = room.Id };
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

    private void WriteSideCar(Room room)
    {
        var data = _projector.Project(room);
        // Neighbors is recommend-context only; clear it so it never serializes.
        // (OmitEmptyCollections then drops the now-empty list entirely.)
        data.Neighbors.Clear();

        var path = SideCarPath(room);
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(path, Serializer.Serialize(data));
    }

    private string SideCarPath(Room room)
    {
        var area = room.Area ?? "";
        var idx = room.Id.IndexOf(':');
        var key = idx >= 0 ? room.Id[(idx + 1)..] : room.Id;
        return Path.Combine(_root, area, "rooms", $"{key}.yaml");
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
