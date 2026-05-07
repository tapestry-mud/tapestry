using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Engine.Login;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Shared;

namespace Tapestry.Server;

public class PlayerSpawner
{
    private static readonly string DefaultRecallRoom = "core:town-square";

    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly GameLoop _gameLoop;
    private readonly GmcpService _gmcpService;
    private readonly MobAIManager _mobAI;
    private readonly SystemEventQueue _eventQueue;
    private readonly TapestryMetrics _metrics;
    private readonly ILogger<PlayerSpawner> _logger;

    public PlayerSpawner(
        SessionManager sessions,
        World world,
        GameLoop gameLoop,
        GmcpService gmcpService,
        MobAIManager mobAI,
        SystemEventQueue eventQueue,
        TapestryMetrics metrics,
        ILogger<PlayerSpawner> logger)
    {
        _sessions = sessions;
        _world = world;
        _gameLoop = gameLoop;
        _gmcpService = gmcpService;
        _mobAI = mobAI;
        _eventQueue = eventQueue;
        _metrics = metrics;
        _logger = logger;
    }

    public void RestoreWorldObjects(PlayerLoadResult data)
    {
        foreach (var (corpse, corpseItems) in data.Corpses)
        {
            if (corpse.LocationRoomId != null)
            {
                var corpseRoom = _world.GetRoom(corpse.LocationRoomId);
                if (corpseRoom != null)
                {
                    corpseRoom.AddEntity(corpse);
                    _world.TrackEntity(corpse);
                    foreach (var item in corpseItems)
                    {
                        _world.TrackEntity(item);
                    }
                }
            }
        }

        foreach (var item in data.AllItems)
        {
            _world.TrackEntity(item);
        }
    }

    public void CompleteLogin(Entity entity, IConnection connection, LoginContext preLogin)
    {
        var spawnRoom = _world.GetRoom(entity.LocationRoomId ?? DefaultRecallRoom)
                        ?? _world.GetRoom(DefaultRecallRoom)
                        ?? _world.AllRooms.FirstOrDefault();

        if (spawnRoom != null)
        {
            if (entity.LocationRoomId != null)
            {
                var currentRoom = _world.GetRoom(entity.LocationRoomId);
                currentRoom?.RemoveEntity(entity);
            }
            spawnRoom.AddEntity(entity);
            _mobAI.PlayerEnteredRoom(spawnRoom.Id);
        }

        _world.TrackEntity(entity);

        var session = new PlayerSession(connection, entity);
        session.Phase = LoginPhase.Playing;

        _sessions.RemovePreLogin(preLogin.ConnectionId);
        _sessions.Add(session);
        _metrics.ActiveConnections.Add(1);

        _logger.LogInformation("Player {Name} connected (entity {Id})", entity.Name, entity.Id);

        connection.SendLine("");
        connection.SendLine("Welcome, " + entity.Name + "!");
        connection.SendLine("");

        session.InputQueue.Enqueue("motd");
        session.InputQueue.Enqueue("look");

        _gmcpService.SendLoginPhase(connection.Id, "playing");

        var capturedConnectionId = connection.Id;
        var capturedEntity = entity;
        _gameLoop.Schedule(() => _gmcpService.SendPostLoginBurst(capturedConnectionId, capturedEntity));

        connection.OnDisconnected += () =>
        {
            _eventQueue.Enqueue(new DisconnectEvent(
                Guid.Parse(connection.Id), entity.Id, "connection closed"));
        };
    }

    public void TakeOverSession(PlayerSession existing, IConnection newConnection, LoginContext preLogin)
    {
        existing.SendLine("Another connection has taken over this character.");
        _sessions.Remove(existing);
        existing.Connection.Disconnect("session takeover");
        _metrics.ActiveConnections.Add(-1);

        CompleteLogin(existing.PlayerEntity, newConnection, preLogin);
    }
}
