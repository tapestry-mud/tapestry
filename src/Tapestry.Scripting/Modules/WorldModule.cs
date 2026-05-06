using Tapestry.Engine;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Races;
using Tapestry.Scripting.Services;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class WorldModule : IJintApiModule
{
    private readonly ApiMessaging _messaging;
    private readonly ApiWorld _worldOps;
    private readonly World _world;
    private readonly GameLoop _gameLoop;
    private readonly ClassRegistry _classRegistry;
    private readonly RaceRegistry _raceRegistry;
    private readonly MobAIManager _mobAIManager;

    public WorldModule(ApiMessaging messaging, ApiWorld worldOps, World world, GameLoop gameLoop, ClassRegistry classRegistry, RaceRegistry raceRegistry, MobAIManager mobAIManager)
    {
        _messaging = messaging;
        _worldOps = worldOps;
        _world = world;
        _gameLoop = gameLoop;
        _classRegistry = classRegistry;
        _raceRegistry = raceRegistry;
        _mobAIManager = mobAIManager;
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
            getRoomName = new Func<string, string?>(_worldOps.GetRoomName),
            getRoomDescription = new Func<string, string?>(_worldOps.GetRoomDescription),
            getOnlinePlayers = new Func<object[]>(_worldOps.GetOnlinePlayers),
            disconnectPlayer = new Action<string>(_worldOps.DisconnectPlayer),
            sendMotd = new Action<string>(_messaging.SendMotd),
            getRoomTags = new Func<string, string[]>(_worldOps.GetRoomTags),
            getRoomArea = new Func<string, string?>(_worldOps.GetRoomArea),
            sameArea = new Func<string, string, bool>(_worldOps.SameArea),
            getExitTarget = new Func<string, string, string?>(_worldOps.GetExitTarget),
            getEntitiesInRoom = new Func<string, string, object[]>(_worldOps.GetEntitiesInRoomByTag),
            getEntity = new Func<string, object?>(_worldOps.GetEntityDetails),
            createEntity = new Func<string, string, string?>(_worldOps.CreateEntity),
            addTag = new Action<string, string>(_worldOps.AddEntityTag),
            hasTag = new Func<string, string, bool>(_worldOps.HasEntityTag),
            send = new Action<string, string>((entityIdStr, text) =>
            {
                if (Guid.TryParse(entityIdStr, out var entityId))
                {
                    _messaging.Send(entityId, text);
                }
            }),
            getProperty = new Func<string, string, object?>((entityIdStr, key) =>
            {
                if (Guid.TryParse(entityIdStr, out var entityId))
                {
                    var entity = _world.GetEntity(entityId);
                    return entity?.GetProperty<object>(key);
                }
                return null;
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
                if (!_classRegistry.Has(classId)) { return; }
                entity.SetProperty("class", classId);
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
            buildInfo = new Func<object>(() => new
            {
                engineSha = Environment.GetEnvironmentVariable("ENGINE_BUILD_SHA") ?? "dev"
            })
        };
    }
}
