using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Login;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Server.Gmcp.Handlers;
using Tapestry.Server.Login;
using Tapestry.Shared;

namespace Tapestry.Server;

public class PlayerSpawner
{
    private static readonly string DefaultRecallRoom = "core:town-square";

    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly GameLoop _gameLoop;
    private readonly LoginHandler _loginHandler;
    private readonly MobAIManager _mobAI;
    private readonly SystemEventQueue _eventQueue;
    private readonly TapestryMetrics _metrics;
    private readonly ILogger<PlayerSpawner> _logger;

    public PlayerSpawner(
        SessionManager sessions,
        World world,
        GameLoop gameLoop,
        LoginHandler loginHandler,
        MobAIManager mobAI,
        SystemEventQueue eventQueue,
        TapestryMetrics metrics,
        ILogger<PlayerSpawner> logger)
    {
        _sessions = sessions;
        _world = world;
        _gameLoop = gameLoop;
        _loginHandler = loginHandler;
        _mobAI = mobAI;
        _eventQueue = eventQueue;
        _metrics = metrics;
        _logger = logger;
    }

    public void RestoreWorldObjects(PlayerLoadResult data)
    {
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

        if (connection.RemoteAddress != null)
        {
            entity.SetProperty("last_ip", connection.RemoteAddress);
        }

        var session = new PlayerSession(connection, entity);
        session.Phase = LoginPhase.Playing;

        connection.OnDisconnected += () =>
        {
            _eventQueue.Enqueue(new DisconnectEvent(
                Guid.Parse(connection.Id), entity.Id, "connection closed"));
        };

        _sessions.RemovePreLogin(preLogin.ConnectionId);
        _sessions.Add(session);
        _metrics.ActiveConnections.Add(1);

        _logger.LogInformation("Player {Name} connected (entity {Id})", entity.Name, entity.Id);

        connection.SendLine("");
        connection.SendLine("Welcome, " + entity.Name + "!");
        connection.SendLine("");

        session.EnqueueInput("motd");
        session.EnqueueInput("look");

        _loginHandler.SendLoginPhase(connection.Id, "playing");

        var capturedConnectionId = connection.Id;
        var capturedEntity = entity;
        _gameLoop.Schedule(() => _loginHandler.TriggerPostLoginBurst(capturedConnectionId, capturedEntity));
    }

    public void TakeOverSession(PlayerSession existing, IConnection newConnection, LoginContext preLogin)
    {
        existing.SendLine("Another connection has taken over this character.");
        _sessions.Remove(existing);
        existing.Connection.Disconnect("session takeover");
        _metrics.ActiveConnections.Add(-1);

        CompleteLogin(existing.PlayerEntity, newConnection, preLogin);
    }

    public PlayerSession CompleteNewCharacter(
        string name,
        string hashedPassword,
        IConnection connection,
        LoginContext preLogin,
        FlowEngine? flowEngine)
    {
        var entity = LoginFlow.CreateNewPlayerEntity(name);
        if (connection.RemoteAddress != null)
        {
            entity.SetProperty("last_ip", connection.RemoteAddress);
        }

        var session = new PlayerSession(connection, entity)
        {
            Phase = LoginPhase.Creating,
            PendingPasswordHash = hashedPassword
        };

        connection.OnDisconnected += () =>
        {
            if (session.Phase == LoginPhase.Creating)
            {
                _sessions.Remove(session);
                _metrics.ActiveConnections.Add(-1);
                _logger.LogInformation("New player {Name} disconnected mid-creation (pre-auth)", name);
            }
        };

        _sessions.RemovePreLogin(preLogin.ConnectionId);
        _sessions.Add(session);
        _metrics.ActiveConnections.Add(1);

        _logger.LogInformation("New player {Name} entering creation flow via pre-auth (entity {Id})", name, entity.Id);

        flowEngine?.Trigger(session, "new_player_connect");
        return session;
    }
}
