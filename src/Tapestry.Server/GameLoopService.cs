using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Prompt;
using Tapestry.Engine.Persistence;
using Tapestry.Shared;

namespace Tapestry.Server;

public class GameLoopService : IHostedService
{
    private readonly GameLoop _gameLoop;
    private readonly SessionManager _sessions;
    private readonly PromptRenderer _promptRenderer;
    private readonly SystemEventQueue _eventQueue;
    private readonly World _world;
    private readonly TapestryMetrics _metrics;
    private readonly ServerConfig _config;
    private readonly PlayerPersistenceService _persistence;
    private readonly AccountService _accountService;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<GameLoopService> _logger;
    private readonly EventBus _eventBus;
    private readonly MobAIManager _mobAI;
    private readonly NotificationQueue _notificationQueue;
    private readonly Tapestry.Server.Gmcp.Handlers.NotificationHandler _notificationHandler;
    private Task? _runTask;

    public GameLoopService(
        GameLoop gameLoop,
        SessionManager sessions,
        PromptRenderer promptRenderer,
        SystemEventQueue eventQueue,
        World world,
        TapestryMetrics metrics,
        ServerConfig config,
        PlayerPersistenceService persistence,
        AccountService accountService,
        IHostApplicationLifetime appLifetime,
        ILogger<GameLoopService> logger,
        EventBus eventBus,
        MobAIManager mobAI,
        NotificationQueue notificationQueue,
        Tapestry.Server.Gmcp.Handlers.NotificationHandler notificationHandler)
    {
        _gameLoop = gameLoop;
        _sessions = sessions;
        _promptRenderer = promptRenderer;
        _eventQueue = eventQueue;
        _world = world;
        _metrics = metrics;
        _config = config;
        _persistence = persistence;
        _accountService = accountService;
        _appLifetime = appLifetime;
        _logger = logger;
        _eventBus = eventBus;
        _mobAI = mobAI;
        _notificationQueue = notificationQueue;
        _notificationHandler = notificationHandler;

        WireEvents();
        _metrics.RegisterWorldCensus(_world.SampleCensus);
    }

    private void WireEvents()
    {
        _gameLoop.ConfigureSlowTickThreshold(_config.Telemetry.AdminChannel.SlowTickThresholdMs);

        _gameLoop.OnSlowTick += (tick, total, events, commands, handlers) =>
        {
            var msg = $"[TICK] Tick {tick}: {total:F0}ms (budget {_config.Server.TickRateMs}ms) - events: {events:F0}ms, commands: {commands:F0}ms, handlers: {handlers:F0}ms\r\n";
            _sessions.SendToTag(_config.Telemetry.AdminChannel.Tag, msg);
        };

        _gameLoop.OnDisconnect += (evt) =>
        {
            var session = _sessions.GetByEntityId(evt.EntityId);
            if (session == null)
            {
                return;
            }

            // Session takeover: the entity is now owned by a different connection.
            // The stale disconnect event from the old connection must not tear down the new session.
            if (Guid.Parse(session.Connection.Id) != evt.SessionId)
            {
                return;
            }

            var playerName = session.PlayerEntity.Name;
            var lastRoomId = session.PlayerEntity.LocationRoomId;
            var isIntentionalQuit = evt.Reason.Equals("Quit", StringComparison.OrdinalIgnoreCase);

            if (!isIntentionalQuit && session.Phase == LoginPhase.Playing && _config.LinkDead.Enabled)
            {
                session.Phase = LoginPhase.LinkDead;
                session.LinkDeadSinceTick = _gameLoop.TickCount;
                session.PlayerEntity.AddTag("linkdead");
                _sessions.RemoveConnectionOnly(session);
                _metrics.LinkDeadActive.Add(1);

                if (lastRoomId != null)
                {
                    _sessions.SendToRoom(lastRoomId, playerName + " has lost their connection.\r\n",
                        session.PlayerEntity.Id);
                }

                _eventBus.Publish(new GameEvent
                {
                    Type = "player.linkdead",
                    SourceEntityId = evt.EntityId,
                    Data = new Dictionary<string, object?> { ["reason"] = evt.Reason }
                });

                _logger.LogInformation("Player {Name} went link-dead: {Reason}", playerName, evt.Reason);
                return;
            }

            _ = _persistence.SavePlayer(session).ContinueWith(
                t => _logger.LogError(t.Exception?.GetBaseException(), "Failed to save player {Name} on disconnect", playerName),
                TaskContinuationOptions.OnlyOnFaulted);

            _sessions.Remove(session);
            _accountService.UntrackOnlineEntity(session.PlayerEntity.Id);
            _metrics.ActiveConnections.Add(-1);

            if (lastRoomId != null)
            {
                var room = _world.GetRoom(lastRoomId);
                room?.RemoveEntity(session.PlayerEntity);
                _mobAI.PlayerLeftRoom(lastRoomId);
            }

            _world.UntrackEntityDeep(session.PlayerEntity);

            _eventBus.Publish(new GameEvent
            {
                Type = "player.logout",
                SourceEntityId = evt.EntityId,
                Data = new Dictionary<string, object?> { ["reason"] = evt.Reason }
            });

            if (!isIntentionalQuit && lastRoomId != null)
            {
                _sessions.SendToRoom(lastRoomId, playerName + " fades from existence.\r\n");
            }

            _logger.LogInformation("Player {Name} disconnected: {Reason}", playerName, evt.Reason);
        };

        var idle = _config.Idle;
        if (idle.WarnSeconds > 0 || idle.TimeoutSeconds > 0)
        {
            _gameLoop.ConfigureIdleTimeout(idle.WarnSeconds, idle.TimeoutSeconds);
            _gameLoop.RegisterIdleTimeoutHandler(_eventQueue, _sessions,
                idle.WarnMessage, idle.TimeoutMessage, idle.AdminTag);
        }

        if (_config.LinkDead.Enabled)
        {
            var ticksPerSecond = (long)Math.Round(1000.0 / _config.Server.TickRateMs);
            var timeoutTicks = _config.LinkDead.TimeoutSeconds * ticksPerSecond;

            _gameLoop.RegisterTickHandler("linkdead-cleanup", 300, () =>
            {
                var currentTick = _gameLoop.TickCount;
                foreach (var session in _sessions.AllLinkDeadSessions.ToList())
                {
                    if (currentTick - session.LinkDeadSinceTick < timeoutTicks)
                    {
                        continue;
                    }

                    var playerName = session.PlayerEntity.Name;
                    var lastRoomId = session.PlayerEntity.LocationRoomId;

                    _ = _persistence.SavePlayer(session).ContinueWith(
                        t => _logger.LogError(t.Exception?.GetBaseException(),
                            "Failed to save player {Name} on linkdead timeout", playerName),
                        TaskContinuationOptions.OnlyOnFaulted);

                    _sessions.Remove(session);
                    _accountService.UntrackOnlineEntity(session.PlayerEntity.Id);
                    _metrics.ActiveConnections.Add(-1);
                    _metrics.LinkDeadActive.Add(-1);
                    _metrics.LinkDeadExpired.Add(1);

                    if (lastRoomId != null)
                    {
                        var room = _world.GetRoom(lastRoomId);
                        room?.RemoveEntity(session.PlayerEntity);
                        _mobAI.PlayerLeftRoom(lastRoomId);
                    }

                    _world.UntrackEntityDeep(session.PlayerEntity);

                    _eventBus.Publish(new GameEvent
                    {
                        Type = "player.logout",
                        SourceEntityId = session.PlayerEntity.Id,
                        Data = new Dictionary<string, object?> { ["reason"] = "linkdead timeout" }
                    });

                    if (lastRoomId != null)
                    {
                        _sessions.SendToRoom(lastRoomId, playerName + " fades from existence.\r\n");
                    }

                    _logger.LogInformation("Player {Name} linkdead timeout expired", playerName);
                }
            });
        }

        _gameLoop.OnNotificationDrain += (sessions, queue) =>
        {
            foreach (var session in sessions.AllSessions)
            {
                var notifications = queue.DrainFor(session.PlayerEntity.Id);
                if (notifications.Count == 0) { continue; }
                foreach (var notification in notifications)
                {
                    sessions.SendToPlayer(session.PlayerEntity.Id, notification.Text);
                }
                _notificationHandler.DrainAndSend(session.PlayerEntity.Id, notifications);
            }
        };

        _gameLoop.OnTickComplete += () =>
        {
            _sessions.FlushPrompts(_promptRenderer);
        };
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Game loop starting. Tick rate: {TickRate}ms", _config.Server.TickRateMs);
        _runTask = _gameLoop.RunAsync(_config.Server.TickRateMs, _appLifetime.ApplicationStopping);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Game loop stopping — saving all players...");

        try
        {
            await _persistence.SaveAllPlayers();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during shutdown save");
        }

        // Notify connected players
        foreach (var session in _sessions.AllSessions)
        {
            session.SendLine("Server shutting down.");
        }

        if (_runTask != null)
        {
            try
            {
                await _runTask;
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }
    }
}
