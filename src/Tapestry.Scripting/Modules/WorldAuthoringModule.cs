using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Items;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Recommend;
using Tapestry.Scripting.Connections;
using Tapestry.Shared;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
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
    private readonly string? _packsRoot;
    private readonly HashSet<string> _loadedPackNamespaces;
    private readonly AreaRegistry _areaRegistry;
    private readonly OracleTableRegistry _oracleRegistry;
    private readonly ItemRegistry? _itemRegistry;
    private readonly RecommendBroker? _recommend;
    private readonly ConnectionLoader? _connections;
    private readonly GameLoop? _gameLoop;
    private readonly StubExitResolver _stubResolver;
    private readonly ILogger<WorldAuthoringModule> _logger;
    private readonly TapestryMetrics? _metrics;
    private readonly RuntimeNamespaceStore? _runtimeNamespaces;
    private const int RecommendMaxInFlight = 2;
    private static int _recommendInFlight;

    // Mirrors ConnectionsModule's serializer, plus OmitEmptyCollections so the
    // recommend-only Neighbors list (cleared before serialization) and any other
    // empty collection (tags/properties/exits) never emit a key. This keeps the
    // side-car a clean pack-room YAML and guarantees no `neighbors:` leaks.
    // ExitDataConverter emits non-stub exits as bare scalars (byte-identical to legacy)
    // and stub exits as { stub: true, label: "..." } mappings.
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(
            DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
        .WithTypeConverter(new Tapestry.Engine.Authoring.ExitDataConverter())
        .Build();

    public WorldAuthoringModule(
        World world,
        RoomProjector projector,
        AttributeWriter writer,
        string root,
        HashSet<string> loadedPackNamespaces,
        AreaRegistry areaRegistry,
        StubExitResolver stubResolver,
        OracleTableRegistry oracleRegistry,
        RecommendBroker? recommend = null,
        ConnectionLoader? connections = null,
        GameLoop? gameLoop = null,
        ILogger<WorldAuthoringModule>? logger = null,
        TapestryMetrics? metrics = null,
        string? packsRoot = null,
        ItemRegistry? itemRegistry = null,
        RuntimeNamespaceStore? runtimeNamespaces = null)
    {
        _world = world;
        _projector = projector;
        _writer = writer;
        _root = root;
        _loadedPackNamespaces = loadedPackNamespaces;
        _areaRegistry = areaRegistry;
        _stubResolver = stubResolver;
        _oracleRegistry = oracleRegistry;
        _itemRegistry = itemRegistry;
        _recommend = recommend;
        _connections = connections;
        _gameLoop = gameLoop;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WorldAuthoringModule>.Instance;
        _metrics = metrics;
        _runtimeNamespaces = runtimeNamespaces;
        _packsRoot = packsRoot;
    }

    public string Namespace => "authoring";

    public const string WipFlag = "wip";

    public object Build(JintEngine jint)
    {
        return new
        {
            createArea = new Func<string, string, bool>((id, name) => CreateArea(id, name)),
            createPack = new Func<string, string?>(CreatePack),
            // Jint exposes CLR members by exact (PascalCase) name; pack JS reads camelCase,
            // so project AreaInfo into a camelCase anon object (matches the ShopModule convention).
            getArea = new Func<string, object>(id =>
            {
                var a = GetArea(id);
                return new
                {
                    id = a.Id,
                    name = a.Name,
                    @short = a.Short,
                    description = a.Description,
                    theme = a.Theme,
                    lore = a.Lore,
                    levelRange = a.LevelRange,
                    resetInterval = a.ResetInterval,
                    sourcePack = a.SourcePack,
                    sideCar = a.SideCar,
                    exists = a.Exists,
                    wip = a.Wip
                };
            }),
            setAreaName = new Func<string, string, bool>((id, v) => SetAreaName(id, v)),
            setAreaShort = new Func<string, string, bool>((id, v) => SetAreaShort(id, v)),
            setAreaDescription = new Func<string, string, bool>((id, v) => SetAreaDescription(id, v)),
            setAreaTheme = new Func<string, string, bool>((id, v) => SetAreaTheme(id, v)),
            setAreaLore = new Func<string, string, bool>((id, v) => SetAreaLore(id, v)),
            setAreaAttribute = new Func<string, string, string, string>((id, a, v) => SetAreaAttribute(id, a, v)),
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
            recommendEnabled = new Func<bool>(() => _recommend?.IsEnabled == true),
            recommend = new Action<JsValue, JsValue>((options, callback) =>
            {
                // Pack already checked recommendEnabled(); double-guard here.
                if (_recommend == null || !_recommend.IsEnabled || _gameLoop == null || options is not ObjectInstance opt)
                {
                    _gameLoop?.Schedule(() => jint.Invoke(callback, JsValue.Null));
                    return;
                }
                var fieldVal = opt.Get("field");
                var field = fieldVal.Type == Types.String ? fieldVal.ToString() : "description";
                var templateVal = opt.Get("template");
                var template = templateVal.Type == Types.String ? templateVal.ToString() : "";
                var systemVal = opt.Get("system");
                var system = systemVal.Type == Types.String ? systemVal.ToString() : null;
                // Optional stringified JSON Schema: when present (and the deployment opts into
                // structured output), the provider requests response_format json_schema and returns
                // raw JSON. Crosses as a string only - no object marshalling over the Jint boundary.
                var schemaVal = opt.Get("schema");
                var schema = schemaVal.Type == Types.String ? schemaVal.ToString() : null;

                // Engine projects the room context (neighbors/area/biome). Empty RoomData when
                // no roomId (e.g. area-creation dressing before any room exists).
                var roomIdVal = opt.Get("roomId");
                var room = roomIdVal.Type == Types.String ? _world.GetRoom(roomIdVal.ToString()) : null;
                var roomData = room != null ? _projector.Project(room) : new RoomData();

                var vars = new Dictionary<string, string>();
                if (opt.Get("vars") is ObjectInstance vobj)
                {
                    foreach (var p in vobj.GetOwnProperties())
                    {
                        vars[p.Key.ToString()] = p.Value.Value?.ToString() ?? "";
                    }
                }

                var ctx = new PackRoomContext { Room = roomData, Template = template, System = system, Vars = vars };

                // Concurrency cap: shed excess calls rather than flood the LLM backend.
                var currentInFlight = System.Threading.Volatile.Read(ref _recommendInFlight);
                if (currentInFlight >= RecommendMaxInFlight)
                {
                    _logger.LogInformation("recommend[{Field}] shed inflight={Inflight}", field, currentInFlight);
                    if (_metrics != null)
                    {
                        var shedTags = new System.Diagnostics.TagList
                        {
                            new KeyValuePair<string, object?>("field", field),
                            new KeyValuePair<string, object?>("outcome", "shed")
                        };
                        _metrics.RecommendTotal.Add(1, shedTags);
                    }
                    _gameLoop.Schedule(() => jint.Invoke(callback, JsValue.Null));
                    return;
                }

                // Off-loop async; deliver the result back on the loop thread via Schedule.
                // Track in-flight count and log latency + outcome at INFO for observability.
                var inflight = System.Threading.Interlocked.Increment(ref _recommendInFlight);
                _metrics?.RecommendInFlight.Add(1);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                _logger.LogInformation("recommend[{Field}] dispatch inflight={Inflight}", field, inflight);
                _ = _recommend.RecommendAsync(new RecommendRequest(field, ctx, ResponseSchema: schema))
                    .ContinueWith(t =>
                    {
                        sw.Stop();
                        var elapsed = (long)sw.Elapsed.TotalMilliseconds;
                        var remaining = System.Threading.Interlocked.Decrement(ref _recommendInFlight);
                        string outcome;
                        if (t.IsFaulted || t.IsCanceled)
                        {
                            outcome = "fault";
                        }
                        else if (t.Result.Suggestions.Count > 0)
                        {
                            outcome = "ok";
                        }
                        else
                        {
                            outcome = "empty";
                        }
                        _logger.LogInformation("recommend[{Field}] {Outcome} {Ms}ms inflight={Inflight}", field, outcome, elapsed, remaining);
                        if (_metrics != null)
                        {
                            var tags = new System.Diagnostics.TagList
                            {
                                new KeyValuePair<string, object?>("field", field),
                                new KeyValuePair<string, object?>("outcome", outcome)
                            };
                            _metrics.RecommendDuration.Record(elapsed, tags);
                            _metrics.RecommendTotal.Add(1, tags);
                            _metrics.RecommendInFlight.Add(-1);
                        }
                        var text = outcome == "ok" ? t.Result.Suggestions[0] : null;
                        _gameLoop.Schedule(() => jint.Invoke(callback, text == null ? JsValue.Null : (JsValue)text));
                    });
            }),
            // Jint exposes CLR members by exact (PascalCase) name; project to camelCase for pack JS.
            // JsValue (not bool): a missing JS arg must mean "include WIP" to match GetAreas's
            // C# default. Jint 4.x marshals a missing arg to CLR null (so `getAreas()` -> null);
            // an explicit `getAreas(undefined)` arrives non-null with Type != Boolean. Both -> true;
            // an explicit JS bool wins. (Type/ToObject() is the codebase-canonical read; no IsBoolean.)
            getAreas = new Func<JsValue, object>(arg => GetAreas(arg == null || arg.Type != Types.Boolean || (bool)arg.ToObject()!).Select(a => new
            {
                id = a.Id,
                name = a.Name,
                @short = a.Short,
                levelRange = a.LevelRange,
                provenance = a.Provenance,
                roomCount = a.RoomCount,
                overrideCount = a.OverrideCount,
                wip = a.Wip
            }).ToArray()),
            getAreaRooms = new Func<string, object>(id => GetAreaRooms(id).Select(r => new
            {
                id = r.Id,
                name = r.Name,
                provenance = r.Provenance
            }).ToArray()),
            setStubExit = new Func<string, string, string, bool>((roomId, direction, label) =>
                SetStubExit(roomId, direction, label)),
            registerStubResolver = new Action<JsValue>(fn =>
            {
                _stubResolver.Register((roomId, dir) =>
                {
                    var r = jint.Invoke(fn, roomId, dir);
                    return r.Type == Types.Boolean && (bool)r.ToObject()!;
                });
            }),
            writeOracleTable = new Action<JsValue>(options =>
            {
                if (options is not ObjectInstance obj) { return; }
                var areaId = obj.Get("areaId").ToString();
                var kind = obj.Get("kind").ToString();
                var table = new OracleTable { Kind = kind };
                if (obj.Get("entries") is JsArray arr)
                {
                    for (uint i = 0; i < arr.Length; i++)
                    {
                        if (arr[(int)i] is not ObjectInstance eo) { continue; }
                        var wVal = eo.Get("w");
                        var idVal = eo.Get("id");
                        var entry = new OracleEntry
                        {
                            W = wVal.Type == Types.Number ? (int)(double)wVal.ToObject()! : 0,
                            Id = idVal.Type == Types.String ? idVal.ToString() : "",
                        };
                        var nameVal = eo.Get("name");
                        if (nameVal.Type == Types.String) { entry.Name = nameVal.ToString(); }
                        var descVal = eo.Get("desc");
                        if (descVal.Type == Types.String) { entry.Desc = descVal.ToString(); }
                        var balanceRefVal = eo.Get("balance_ref");
                        if (balanceRefVal.Type == Types.String) { entry.BalanceRef = balanceRefVal.ToString(); }
                        var rarityVal = eo.Get("rarity");
                        if (rarityVal.Type == Types.String) { entry.Rarity = rarityVal.ToString(); }
                        table.Entries.Add(entry);
                    }
                }
                WriteOracleTableSideCar(areaId, table);
            }),
            writeItemTemplate = new Func<JsValue, object?>(options =>
            {
                if (options is not ObjectInstance obj) { return null; }
                var areaId = obj.Get("areaId").ToString();
                var id = obj.Get("id").ToString();
                var baseId = obj.Get("base").ToString();
                var name = obj.Get("name").ToString();
                var descVal = obj.Get("desc");
                var desc = descVal.Type == Types.String ? descVal.ToString() : "";
                var typeVal = obj.Get("type");
                var type = typeVal.Type == Types.String ? typeVal.ToString() : null;
                var props = ToClrProperties(obj.Get("properties"));
                return WriteItemTemplateSideCar(areaId, id, baseId, name, desc, type, props);
            })
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

    private bool SetStubExit(string roomId, string direction, string label)
    {
        var room = _world.GetRoom(roomId);
        if (room == null || !IsOracleArea(room.Area)) { return false; }
        if (!DirectionExtensions.TryParse(direction ?? "", out var dir)) { return false; }
        room.SetExit(dir, new Exit("") { IsStub = true, DisplayName = label });
        WriteSideCar(room);
        return true;
    }

    private bool IsOracleArea(string? areaId)
    {
        if (string.IsNullOrEmpty(areaId)) { return false; }
        var areaDef = _areaRegistry.Get(areaId);
        return areaDef != null && string.IsNullOrEmpty(areaDef.SourcePack);
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
        return Path.Combine(_root, SafeSegment(area), "rooms", $"{key}.yaml");
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

    // Mirrors AreaSideCarPath/SideCarPath - _root is already data/areas, NO "areas" literal, SafeSegment applied.
    // places  -> <_root>/<areaId>/places-oracle.yaml
    // other   -> <_root>/<areaId>/<kind>/<singular>-oracle-table.yaml  (e.g. mobs/mob-oracle-table.yaml)
    private string OracleTableSideCarPath(string areaId, string kind)
    {
        if (kind == "places")
        {
            return Path.Combine(_root, SafeSegment(areaId), "places-oracle.yaml");
        }
        var singular = kind.EndsWith("s", StringComparison.Ordinal) ? kind[..^1] : kind;
        return Path.Combine(_root, SafeSegment(areaId), kind, $"{singular}-oracle-table.yaml");
    }

    /// <summary>Registers <paramref name="table"/> into the live <see cref="OracleTableRegistry"/>
    /// (same-session visibility) then writes its sidecar YAML file under the authoring root.
    /// Returns the path written. This is the freeze step the generator calls once at creation.</summary>
    public string WriteOracleTableSideCar(string areaId, OracleTable table)
    {
        // Register into the live registry first so tapestry.oracle.table(...) resolves in
        // the same solo run - without this the new area mints empty until the next reboot.
        table.Id = OracleTable.OracleTableId(areaId, table.Kind);
        table.SourcePack = areaId;
        _oracleRegistry.Register(table);

        var path = OracleTableSideCarPath(areaId, table.Kind);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) { Directory.CreateDirectory(dir); }
        File.WriteAllText(path, YamlContentLoader.SerializeOracleTable(table));
        return path;
    }

    // Mirrors OracleTableSideCarPath - _root is already data/areas, NO "areas" literal, SafeSegment applied.
    private string ItemTemplateSideCarPath(string areaId, string id)
    {
        var shortId = id.Contains(':') ? id[(id.LastIndexOf(':') + 1)..] : id;
        return Path.Combine(_root, SafeSegment(areaId), "items", $"{shortId}.yaml");
    }

    // Returns the registered id, or null if the base template is unknown or ItemRegistry not wired.
    public string? WriteItemTemplateSideCar(
        string areaId, string id, string baseId, string name, string desc,
        string? type, Dictionary<string, object?> properties)
    {
        if (_itemRegistry == null) { return null; }
        var baseTemplate = _itemRegistry.GetTemplate(baseId);
        if (baseTemplate == null) { return null; }

        // Inherit static identity from the base; overlay rolled fields.
        var mergedRaw = new Dictionary<string, object?>(baseTemplate.Properties);
        foreach (var kv in properties) { mergedRaw[kv.Key] = kv.Value; }
        mergedRaw["description"] = desc;
        // Coerce any nested all-numeric dict to Dictionary<string,int> so GetProperty<Dictionary<string,int>>("ac") resolves.
        var merged = NormalizeClrProperties(mergedRaw);

        var template = new ItemTemplate
        {
            Id = id,
            Name = name,
            Type = string.IsNullOrEmpty(type) ? baseTemplate.Type : type!,
            Tags = new List<string>(baseTemplate.Tags),
            Keywords = new List<string>(baseTemplate.Keywords),
            Properties = merged,
        };
        _itemRegistry.Register(template);

        var path = ItemTemplateSideCarPath(areaId, id);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) { Directory.CreateDirectory(dir); }
        File.WriteAllText(path, YamlContentLoader.SerializeItemDefinition(
            id, name, template.Type, template.Keywords, template.Tags, merged));
        return id;
    }

    // Converts a JS properties object to a CLR Dictionary<string,object?>.
    // Nested all-numeric JS objects (e.g. the ac map) become Dictionary<string,int>
    // so GetProperty<Dictionary<string,int>>("ac") resolves on an exact-type match.
    private static Dictionary<string, object?> ToClrProperties(JsValue value)
    {
        var result = new Dictionary<string, object?>();
        if (value is not ObjectInstance obj) { return result; }
        foreach (var prop in obj.GetOwnProperties())
        {
            var k = prop.Key.ToString();
            var v = prop.Value.Value;
            if (v is ObjectInstance nested && v is not JsArray)
            {
                var intMap = new Dictionary<string, int>();
                var ok = true;
                foreach (var np in nested.GetOwnProperties())
                {
                    var nv = np.Value.Value;
                    if (nv.Type == Types.Number) { intMap[np.Key.ToString()] = (int)(double)nv.ToObject()!; }
                    else { ok = false; break; }
                }
                result[k] = ok ? (object?)intMap : nested.ToString();
            }
            else if (v.Type == Types.Number) { result[k] = (double)v.ToObject()!; }
            else if (v.Type == Types.Boolean) { result[k] = (bool)v.ToObject()!; }
            else { result[k] = v.ToString(); }
        }
        return result;
    }

    // Coerces any nested Dictionary<string,object?> whose values are all numeric
    // to Dictionary<string,int>, mirroring what ToClrProperties does for the JS path.
    private static Dictionary<string, object?> NormalizeClrProperties(Dictionary<string, object?> props)
    {
        var result = new Dictionary<string, object?>(props.Count);
        foreach (var kv in props)
        {
            if (kv.Value is Dictionary<string, object?> nested)
            {
                var intMap = new Dictionary<string, int>(nested.Count);
                var allInts = true;
                foreach (var nkv in nested)
                {
                    if (nkv.Value is int i) { intMap[nkv.Key] = i; }
                    else if (nkv.Value is long l) { intMap[nkv.Key] = (int)l; }
                    else if (nkv.Value is double d) { intMap[nkv.Key] = (int)d; }
                    else { allInts = false; break; }
                }
                result[kv.Key] = allInts ? (object?)intMap : kv.Value;
            }
            else { result[kv.Key] = kv.Value; }
        }
        return result;
    }

    private static string SlugToName(string areaId)
    {
        var idPart = areaId.Contains(':') ? areaId[(areaId.IndexOf(':') + 1)..] : areaId;
        var words = idPart.Replace('-', ' ').Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var joined = string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
        return string.IsNullOrWhiteSpace(joined) ? areaId : joined;
    }

    private bool MutateArea(string areaId, Action<AreaDefinition> mutate)
    {
        var def = _areaRegistry.Get(areaId);
        if (def == null)
        {
            return false;
        }
        mutate(def);
        _areaRegistry.Register(def);
        WriteAreaSideCar(def);
        return true;
    }

    public bool SetAreaName(string areaId, string name)        { return MutateArea(areaId, d => d.Name = name); }
    public bool SetAreaShort(string areaId, string text)       { return MutateArea(areaId, d => d.Short = text); }
    public bool SetAreaDescription(string areaId, string text) { return MutateArea(areaId, d => d.Description = text); }
    public bool SetAreaTheme(string areaId, string text)       { return MutateArea(areaId, d => d.Theme = text); }
    public bool SetAreaLore(string areaId, string text)        { return MutateArea(areaId, d => d.Lore = text); }

    public string SetAreaAttribute(string areaId, string attr, string value)
    {
        var def = _areaRegistry.Get(areaId);
        if (def == null)
        {
            return "No such area: " + areaId;
        }
        switch (attr)
        {
            case "level_range":
            {
                var parts = value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 || !int.TryParse(parts[0], out var lo) || !int.TryParse(parts[1], out var hi))
                {
                    return "level_range expects \"min,max\" (e.g. 5,12).";
                }
                def.LevelRange = new[] { lo, hi };
                break;
            }
            case "reset_interval":
            {
                if (!int.TryParse(value, out var ri))
                {
                    return "reset_interval expects an integer (seconds).";
                }
                def.ResetInterval = ri;
                break;
            }
            case "wip":
            {
                if (!bool.TryParse(value, out var on))
                {
                    return "wip expects true or false.";
                }
                if (on)
                {
                    if (!def.Flags.Contains(WipFlag)) { def.Flags.Add(WipFlag); }
                }
                else
                {
                    def.Flags.RemoveAll(f => f == WipFlag);
                }
                break;
            }
            case "seed":
            {
                if (!long.TryParse(value, out var seed)) { return $"Invalid seed: {value}"; }
                def.Seed = seed;
                break;
            }
            default:
            {
                return "Unknown area attribute: " + attr;
            }
        }
        _areaRegistry.Register(def);
        WriteAreaSideCar(def);
        return "Set " + attr + ".";
    }

    public IReadOnlyList<AreaSummary> GetAreas(bool includeWip = true)
    {
        var list = new List<AreaSummary>();
        foreach (var def in _areaRegistry.All())
        {
            var wip = def.Flags.Contains(WipFlag);
            if (wip && !includeWip)
            {
                continue;
            }
            var areaSideCar = File.Exists(AreaSideCarPath(def.Id));
            var provenance = ProvenanceClassifier.Classify(def.SourcePack, areaSideCar);
            var rooms = _world.AllRooms
                .Where(r => string.Equals(r.Area, def.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var overrideCount = rooms.Count(r =>
            {
                if (string.IsNullOrEmpty(r.GetProperty<string>(CommonProperties.SourcePack)))
                {
                    return false;
                }
                return File.Exists(SideCarPath(r));
            });
            list.Add(new AreaSummary(
                def.Id, def.Name, def.Short ?? "", def.LevelRange,
                provenance, rooms.Count, overrideCount, wip));
        }
        return list
            .OrderBy(a => a.LevelRange is { Length: > 0 } ? a.LevelRange[0] : int.MaxValue)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<RoomSummary> GetAreaRooms(string areaId)
    {
        var rooms = _world.AllRooms
            .Where(r => string.Equals(r.Area, areaId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Id, StringComparer.OrdinalIgnoreCase);
        var list = new List<RoomSummary>();
        foreach (var room in rooms)
        {
            var sourcePack = room.GetProperty<string>(CommonProperties.SourcePack);
            var sideCarExists = File.Exists(SideCarPath(room));
            var provenance = ProvenanceClassifier.Classify(sourcePack, sideCarExists);

            // Orphan detection (spec B 5.4): check Dangling, not Loaded. A dangling record
            // is one where ApplyConnection returned false (a room was missing at Load time).
            // If the record were in Loaded, both rooms existed at boot and the room is
            // reachable - no orphan. Dangling is exactly where the orphan evidence lives.
            if (sourcePack == null && _connections != null)
            {
                var orphaned = _connections.Dangling.Any(rec =>
                    rec.From.Room == room.Id || rec.To.Room == room.Id);
                if (orphaned)
                {
                    provenance = provenance + " (orphaned)";
                }
            }

            list.Add(new RoomSummary(room.Id, room.Name, provenance));
        }
        return list;
    }

    /// <summary>
    /// Creates a destination pack for solo-generated content: registers its namespace into the
    /// live loaded-namespaces set (so a post-boot createRoom in that namespace is accepted) AND
    /// persists a minimal world-pack manifest under the packs root (so the namespace re-registers
    /// on reboot and harvest has a real pack to fold). Idempotent: if the namespace is already
    /// loaded (a pre-existing pack, or a prior call), it returns the namespace without writing.
    /// Returns the registered namespace, or null for an empty name.
    /// </summary>
    public string? CreatePack(string packName)
    {
        if (string.IsNullOrWhiteSpace(packName))
        {
            return null;
        }
        var ns = PackLoader.PackNamespace(packName);
        if (_loadedPackNamespaces.Contains(ns))
        {
            return ns;
        }
        // Best-effort: write a pack scaffold so a binary-mode boot re-discovers the pack.
        // In the docker deployment the packs dir is not writable by the engine uid, so this
        // throws - that is fine: the namespace is persisted to the writable data marker below
        // (RuntimeNamespaceStore) and re-registered at boot, and the generated content lives
        // in data side-cars loaded by the Authored*Loaders independently of this scaffold.
        if (!string.IsNullOrEmpty(_packsRoot))
        {
            try
            {
                var dir = DestinationPackDir(_packsRoot, packName);
                Directory.CreateDirectory(dir);
                var manifestPath = Path.Combine(dir, "pack.yaml");
                if (!File.Exists(manifestPath))
                {
                    File.WriteAllText(manifestPath, BuildDestinationManifest(packName));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning("CreatePack: scaffold write for {Pack} to {Root} failed ({Msg}); relying on the runtime-namespace marker.", packName, _packsRoot, ex.Message);
            }
        }
        // Register + persist the namespace via the writable data marker (docker-safe).
        // Falls back to the in-memory set when the store is absent (e.g. unit tests).
        if (_runtimeNamespaces != null)
        {
            _runtimeNamespaces.Register(ns);
        }
        else
        {
            _loadedPackNamespaces.Add(ns);
        }
        return ns;
    }

    // packsRoot/@scope/name for a scoped name; packsRoot/name otherwise. Mirrors the on-disk
    // layout DiscoverPackDirectories expects so the written pack reloads next boot.
    private static string DestinationPackDir(string packsRoot, string packName)
    {
        if (packName.Contains('/'))
        {
            var parts = packName.Split('/', 2);
            return Path.Combine(packsRoot, parts[0], parts[1]);
        }
        return Path.Combine(packsRoot, packName);
    }

    private static string BuildDestinationManifest(string packName)
    {
        var display = packName.Contains('/') ? packName.Split('/', 2)[1] : packName;
        return string.Join("\n", new[]
        {
            $"name: \"{packName}\"",
            "version: \"0.0.1\"",
            "type: world",
            $"display_name: \"{display}\"",
            "description: \"Generated solo oracle destination pack.\"",
            "author: \"Tapestry\"",
            "license: \"AGPL-3.0\"",
            "engine: \">=0.0.1\"",
            "validation: lenient",
            "active: true",
            "load_order: 900",
            "dependencies:",
            "  \"@tapestry/oracle\": \"^0.1.0\"",
            "content:",
            "  area_definitions: \"areas/**/area.yaml\"",
            "  rooms: \"areas/**/rooms/*.yaml\"",
            "  oracle: \"areas/**/*-oracle-table.yaml\"",
            "  mobs: \"areas/**/mobs/*.yaml\"",
            "  items: \"areas/**/items/*.yaml\"",
            "",
        });
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
            def.LevelRange, def.ResetInterval, def.SourcePack, sideCar, true, def.Flags.Contains(WipFlag));
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
