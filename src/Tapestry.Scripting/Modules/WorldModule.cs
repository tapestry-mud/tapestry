using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Mapping;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Races;
using Tapestry.Engine.Tags;
using Tapestry.Scripting.Services;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public record BuildInfo(string EngineSha, string EngineVersion, string PackBuildRef);

public class WorldModule : IJintApiModule
{
    private readonly ApiMessaging _messaging;
    private readonly ApiWorld _worldOps;
    private readonly World _world;
    private readonly GameLoop _gameLoop;
    private readonly ClassRegistry _classRegistry;
    private readonly RaceRegistry _raceRegistry;
    private readonly MobAIManager _mobAIManager;
    private readonly IGmcpModuleAdapter _gmcp;
    private readonly TagRegistry _tagRegistry;
    private readonly PropertyRegistry _propertyRegistry;
    private readonly AreaMapProjector _areaMapProjector;
    private readonly AsciiMapRenderer _mapRenderer;

    public WorldModule(ApiMessaging messaging, ApiWorld worldOps, World world, GameLoop gameLoop, ClassRegistry classRegistry, RaceRegistry raceRegistry, MobAIManager mobAIManager, IGmcpModuleAdapter gmcp, TagRegistry tagRegistry, PropertyRegistry propertyRegistry, AreaMapProjector areaMapProjector, AsciiMapRenderer mapRenderer)
    {
        _messaging = messaging;
        _worldOps = worldOps;
        _world = world;
        _gameLoop = gameLoop;
        _classRegistry = classRegistry;
        _raceRegistry = raceRegistry;
        _mobAIManager = mobAIManager;
        _gmcp = gmcp;
        _tagRegistry = tagRegistry;
        _propertyRegistry = propertyRegistry;
        _areaMapProjector = areaMapProjector;
        _mapRenderer = mapRenderer;
    }

    public static BuildInfo GetBuildInfo()
    {
        return new BuildInfo(
            EngineSha: Environment.GetEnvironmentVariable("ENGINE_BUILD_SHA") ?? "dev",
            EngineVersion: Environment.GetEnvironmentVariable("ENGINE_BUILD_VERSION") ?? "dev",
            PackBuildRef: Environment.GetEnvironmentVariable("PACK_BUILD_REF") ?? "dev"
        );
    }

    public string Namespace => "world";

    public object Build(JintEngine engine)
    {
        return new
        {
            moveEntity = new Func<string, string, bool>(_worldOps.MoveEntity),
            teleportEntity = new Func<string, string, bool>(_worldOps.TeleportEntity),
            sendRoomDescription = new Action<string>(_messaging.SendRoomDescription),
            sendToRoomExcept = new Action<string, string, string>(_messaging.SendToRoomExcept),
            sendToRoomExceptMany = new Action<string, object[], string>((roomId, excludeArray, text) =>
            {
                var excludeIds = new string[excludeArray.Length];
                for (var i = 0; i < excludeArray.Length; i++)
                {
                    excludeIds[i] = excludeArray[i]?.ToString() ?? "";
                }
                _messaging.SendToRoomExceptMany(roomId, excludeIds, text);
            }),
            sendToRoomExceptSleeping = new Action<string, string, string>(_messaging.SendToRoomSkipSleeping),
            sendToAll = new Action<string, string>(_messaging.SendToAll),
            getEntityRoomId = new Func<string, string?>(_worldOps.GetEntityRoomId),
            getRoomExits = new Func<string, string[]>(_worldOps.GetRoomExits),
            getRoomExitsById = new Func<string, string[]>(_worldOps.GetRoomExitsById),
            getRoomName = new Func<string, string?>(_worldOps.GetRoomName),
            getRoomDescription = new Func<string, string?>(_worldOps.GetRoomDescription),
            getOnlinePlayers = new Func<object[]>(_worldOps.GetOnlinePlayers),
            disconnectPlayer = new Action<string>(_worldOps.DisconnectPlayer),
            sendMotd = new Action<string>(_messaging.SendMotd),
            getRoomTags = new Func<string, string[]>(_worldOps.GetRoomTags),
            getRoomArea = new Func<string, string?>(_worldOps.GetRoomArea),
            getRoomsInArea = new Func<string, string[]>(_worldOps.GetRoomsInArea),
            getRoomProperties = new Func<string, object>(_worldOps.GetRoomProperties),
            getRoomOccupants = new Func<string, object[]>(_worldOps.GetRoomOccupants),
            getRoomBiome = new Func<string, string?>(roomId =>
            {
                var tags = _worldOps.GetRoomTags(roomId);
                if (tags.Length == 0) { return null; }

                // Room tags are stored as bare names (e.g. "forest"), but pack tags are
                // registered under their full scoped key ("tapestry-biomes:forest").
                // TryResolve(tag, null) only does a direct key lookup and skips dep
                // resolution when currentPack is null — it would miss every pack tag.
                // Match against GetAll() by bare Name instead.
                var biomeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in _tagRegistry.GetAll())
                {
                    if (entry.Kind == "biome")
                    {
                        biomeNames.Add(entry.Name);
                        biomeNames.Add(entry.FullName);
                    }
                }

                foreach (var tag in tags)
                {
                    if (biomeNames.Contains(tag))
                    {
                        return tag;
                    }
                }
                return null;
            }),
            sameArea = new Func<string, string, bool>(_worldOps.SameArea),
            getExitTarget = new Func<string, string, string?>(_worldOps.GetExitTarget),
            getEntitiesInRoom = new Func<string, string, object[]>(_worldOps.GetEntitiesInRoomByTag),
            getVisibleEntities = new Func<string, string, object[]>((roomId, observerEntityId) =>
            {
                return _worldOps.GetVisibleEntities(roomId, observerEntityId);
            }),
            getEntity = new Func<string, object?>(_worldOps.GetEntityDetails),
            createEntity = new Func<string, string, string?>(_worldOps.CreateEntity),
            addTag = new Action<string, string>(_worldOps.AddEntityTag),
            hasTag = new Func<string, string, bool>(_worldOps.HasEntityTag),
            hasRole = new Func<string, string, bool>(_worldOps.HasEntityRole),
            send = new Action<string, string>((entityIdStr, text) =>
            {
                if (Guid.TryParse(entityIdStr, out var entityId))
                {
                    _messaging.Send(entityId, text);
                }
            }),
            getProperty = new Func<string, string, object?>((entityIdStr, key) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return null;
                }

                var entity = _world.GetEntity(entityId);
                if (entity == null)
                {
                    return null;
                }

                var value = entity.GetProperty<object>(key);

                // Marshal CLR list/collection values into real Jint arrays so that
                // Array.isArray(...) returns true in JS for list_string properties
                // regardless of whether the value was set this session or reloaded
                // from disk (both paths store a CLR List<string>).
                if (value is System.Collections.IList list && value is not string)
                {
                    var items = new JsValue[list.Count];
                    for (var i = 0; i < list.Count; i++)
                    {
                        var item = list[i];
                        items[i] = item switch
                        {
                            null => JsValue.Null,
                            string s => new JsString(s),
                            bool b => b ? JsBoolean.True : JsBoolean.False,
                            int n => new JsNumber(n),
                            double d => new JsNumber(d),
                            _ => JsValue.FromObject(engine, item)
                        };
                    }
                    return engine.Intrinsics.Array.ConstructFast(items);
                }

                return value;
            }),
            setProperty = new Action<string, string, object?>(_worldOps.SetEntityProperty),
            placeEntity = new Action<string, string>(_worldOps.PlaceEntityInRoom),
            removeEntity = new Action<string>(_worldOps.RemoveEntity),
            getCurrentTick = new Func<long>(() => _gameLoop.TickCount),
            sendToRoom = new Action<string, string>(_messaging.SendToRoom),
            purgeEntities = new Func<string, string, int>(_worldOps.PurgeEntities),
            setClass = new Action<string, string>((entityIdStr, classId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return; }
                var entity = _world.GetEntity(entityId);
                if (entity == null) { return; }
                var def = _classRegistry.Get(classId);
                if (def == null) { return; }
                entity.SetProperty("class", classId);
                _gmcp.Send(entityId, "Response.Char.Class", new
                {
                    class_id = classId,
                    class_name = def.Name,
                    track = def.Track
                });
            }),
            setRace = new Action<string, string>((entityIdStr, raceId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return; }
                var entity = _world.GetEntity(entityId);
                if (entity == null) { return; }
                var def = _raceRegistry.Get(raceId);
                if (def == null) { return; }
                entity.SetProperty("race", raceId);
                foreach (var flag in def.RacialFlags)
                {
                    entity.AddTag(flag);
                }
            }),
            getEntityTags = new Func<string, string[]>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return Array.Empty<string>(); }
                var e = _world.GetEntity(entityId);
                return e == null ? Array.Empty<string>() : e.Tags.ToArray();
            }),
            triggerDisposition = new Action<string>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return; }
                var entity = _world.GetEntity(entityId);
                if (entity == null || entity.LocationRoomId == null) { return; }
                _mobAIManager.TriggerDisposition(entity.LocationRoomId, entityId);
            }),
            findEntityByTag = new Func<string, string?>(tag =>
            {
                var entity = _world.GetEntitiesByTag(tag).FirstOrDefault();
                return entity?.Id.ToString();
            }),
            findPlayerByName = new Func<string, object?>(_worldOps.FindPlayerByName),
            removeTag = new Action<string, string>((entityIdStr, tag) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return; }
                var entity = _world.GetEntity(entityId);
                if (entity != null)
                {
                    entity.RemoveTag(tag);
                }
            }),
            getEntitiesByTag = new Func<string, object[]>(tag =>
            {
                var entities = _world.GetEntitiesByTag(tag);
                return entities.Select(e => new
                {
                    id = e.Id.ToString(),
                    name = e.Name,
                    type = e.Type
                }).ToArray();
            }),
            getEntityKeywords = new Func<string, string[]>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return Array.Empty<string>(); }
                var e = _world.GetEntity(entityId);
                return e == null ? Array.Empty<string>() : e.Keywords.ToArray();
            }),
            getEntityRoles = new Func<string, string[]>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return Array.Empty<string>(); }
                var e = _world.GetEntity(entityId);
                return e == null ? Array.Empty<string>() : e.Roles.ToArray();
            }),
            addRole = new Action<string, string>((entityIdStr, role) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return; }
                var e = _world.GetEntity(entityId);
                if (e != null)
                {
                    e.AddRole(role);
                }
            }),
            removeRole = new Action<string, string>((entityIdStr, role) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return; }
                var e = _world.GetEntity(entityId);
                if (e != null)
                {
                    e.RemoveRole(role);
                }
            }),
            getEntityDisposition = new Func<string, string?>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return null; }
                var e = _world.GetEntity(entityId);
                return e?.Disposition.ToString().ToLower();
            }),
            getEntityType = new Func<string, string?>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return null; }
                var e = _world.GetEntity(entityId);
                return e?.Type;
            }),
            getTagRegistry = new Func<object[]>(() =>
            {
                return _tagRegistry.GetAll()
                    .Select(e => new
                    {
                        name = e.Name,
                        scope = e.Scope,
                        description = e.Description,
                        appliesTo = e.AppliesTo.ToArray(),
                        fullName = e.FullName,
                        isEngine = e.IsEngineTag,
                        kind = e.Kind
                    })
                    .ToArray();
            }),
            getPropertyRegistry = new Func<object[]>(() =>
            {
                return _propertyRegistry.GetAll()
                    .Select(e => new
                    {
                        name = e.Name,
                        scope = e.Scope,
                        description = e.Description,
                        appliesTo = e.AppliesTo?.ToArray() ?? Array.Empty<string>(),
                        fullName = e.FullName,
                        isEngine = e.IsEngineProperty,
                        valueType = AttributeWriter.ValueTypeName(e.ValueType),
                        transient = e.Transient,
                        min = e.Min,
                        max = e.Max,
                        @enum = e.EnumValues?.ToArray()
                    })
                    .ToArray();
            }),
            isTagKnown = new Func<string, string?, bool>((tag, packContext) =>
            {
                return _tagRegistry.IsKnown(tag, packContext);
            }),
            getAllEntities = new Func<object[]>(() =>
            {
                return _world.GetAllTrackedEntities()
                    .Select(e => new
                    {
                        id = e.Id.ToString(),
                        name = e.Name,
                        type = e.Type,
                        tags = e.Tags.ToArray(),
                        templateId = e.TryGetProperty<string>("template_id", out var tid) ? tid : null
                    })
                    .ToArray();
            }),
            buildInfo = new Func<object>(() =>
            {
                var info = GetBuildInfo();
                return new
                {
                    engineSha = info.EngineSha,
                    engineVersion = info.EngineVersion,
                    packBuildRef = info.PackBuildRef
                };
            }),
            renderAreaMap = new Func<string, JsValue, string>((rootRoomId, optsVal) =>
            {
                var room = _world.GetRoom(rootRoomId);
                if (room == null)
                {
                    return "There is nothing to map here.";
                }
                var (scope, viewOpts) = ParseMapOptions(rootRoomId, optsVal);
                if (scope.MaxHops == null && string.IsNullOrEmpty(room.Area))
                {
                    return "There is nothing to map here.";
                }
                var map = _areaMapProjector.Project(room, scope);

                // Render the plane the viewer is standing on. Whole-area projections root
                // deterministically (not at the viewer), so the viewer's room may sit on a
                // non-zero z-plane; radius projections root at the viewer (always plane 0).
                var currentCell = map.Cells.FirstOrDefault(c => c.Id == rootRoomId);
                if (currentCell != null)
                {
                    viewOpts = viewOpts with { Plane = currentCell.Z };
                }

                return _mapRenderer.Render(map, viewOpts);
            }),
            projectArea = new Func<string, JsValue, object?>((rootRoomId, optsVal) =>
            {
                var room = _world.GetRoom(rootRoomId);
                if (room == null)
                {
                    return null;
                }
                var (scope, _) = ParseMapOptions(rootRoomId, optsVal);
                var map = _areaMapProjector.Project(room, scope);
                return new
                {
                    areaId = map.AreaId,
                    rootRoomId = map.RootRoomId,
                    unpositionedRoomIds = map.UnpositionedRoomIds.ToArray(),
                    cells = map.Cells.Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        x = c.X,
                        y = c.Y,
                        z = c.Z,
                        exits = c.Exits.ToArray(),
                        markers = c.Markers.ToArray(),
                        hasVertical = c.HasVertical,
                        collision = c.Collision
                    }).ToArray()
                };
            })
        };
    }

    /// <summary>Unpack the JS opts object: { scope: 'area'|'radius', radius: n,
    /// label: 'id'|'name'|'dot', showCurrent: bool, legend: { markerKey: glyph } }.
    /// Mirrors CommandsModule's obj.GetOwnProperties() idiom for JS-object parsing:
    /// type checks via Types.*, casts via .ToObject()!, string values via .ToString().</summary>
    private static (MapScope Scope, ViewOptions Opts) ParseMapOptions(string currentRoomId, JsValue optsVal)
    {
        var scope = MapScope.WholeArea;
        var label = LabelMode.Dot;
        var showCurrent = true;
        var legend = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (optsVal is ObjectInstance obj)
        {
            var scopeVal = obj.Get("scope");
            if (scopeVal.Type == Types.String
                && string.Equals(scopeVal.ToString(), "radius", StringComparison.OrdinalIgnoreCase))
            {
                var radiusVal = obj.Get("radius");
                var radius = radiusVal.Type == Types.Number ? (int)(double)radiusVal.ToObject()! : 3;
                if (radius <= 0)
                {
                    // NaN casts to 0, Infinity to int.MinValue, and negatives are meaningless —
                    // fall back to the default 3-hop radius rather than a degenerate 1-room map.
                    radius = 3;
                }
                scope = MapScope.Radius(radius);
            }

            var labelVal = obj.Get("label");
            if (labelVal.Type == Types.String)
            {
                label = labelVal.ToString().ToLowerInvariant() switch
                {
                    "id" => LabelMode.Id,
                    "name" => LabelMode.Name,
                    _ => LabelMode.Dot,
                };
            }

            var showVal = obj.Get("showCurrent");
            if (showVal.Type == Types.Boolean)
            {
                showCurrent = (bool)showVal.ToObject()!;
            }

            var legendVal = obj.Get("legend");
            if (legendVal is ObjectInstance legendObj)
            {
                foreach (var prop in legendObj.GetOwnProperties())
                {
                    var keyStr = prop.Key.ToString();
                    var glyph = prop.Value.Value?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(keyStr) && !string.IsNullOrEmpty(glyph))
                    {
                        legend[keyStr] = glyph;
                    }
                }
            }
        }

        var viewOpts = new ViewOptions
        {
            CurrentRoomId = currentRoomId,
            Label = label,
            Legend = legend,
            ShowCurrent = showCurrent,
            Plane = 0,
        };
        return (scope, viewOpts);
    }
}
