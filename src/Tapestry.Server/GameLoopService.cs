using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tapestry.Data;
using Tapestry.Engine;
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
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<GameLoopService> _logger;
    private readonly EventBus _eventBus;
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
        IHostApplicationLifetime appLifetime,
        ILogger<GameLoopService> logger,
        EventBus eventBus)
    {
        _gameLoop = gameLoop;
        _sessions = sessions;
        _promptRenderer = promptRenderer;
        _eventQueue = eventQueue;
        _world = world;
        _metrics = metrics;
        _config = config;
        _persistence = persistence;
        _appLifetime = appLifetime;
        _logger = logger;
        _eventBus = eventBus;

        WireEvents();
    }

    private void WireEvents()
    {
        _gameLoop.ConfigureSlowTickThreshold(_config.Telemetry.AdminChannel.SlowTickThresholdMs);

        _gameLoop.OnSlowTick += (tick, total, events, commands, handlers) =>
        {
            var msg = $"[TICK] Tick {tick}: {total:F0}ms (budget {_config.Server.TickRateMs}ms) — events: {events:F0}ms, commands: {commands:F0}ms, handlers: {handlers:F0}ms\r\n";
            _sessions.SendToTag(_config.Telemetry.AdminChannel.Tag, msg);
        };

        _gameLoop.OnDisconnect += (evt) =>
        {
            var session = _sessions.GetByEntityId(evt.EntityId);
            if (session == null)
            {
                return;
            }

            // Snapshot is synchronous (LocationRoomId still set); only the file write is async
            var playerName = session.PlayerEntity.Name;
            _ = _persistence.SavePlayer(session).ContinueWith(
                t => _logger.LogError(t.Exception?.GetBaseException(), "Failed to save player {Name} on disconnect", playerName),
                TaskContinuationOptions.OnlyOnFaulted);
            _persistence.UntrackPasswordHash(evt.EntityId);
            var lastRoomId = session.PlayerEntity.LocationRoomId;

            _sessions.Remove(session);
            _metrics.ActiveConnections.Add(-1);

            if (session.PlayerEntity.LocationRoomId != null)
            {
                var room = _world.GetRoom(session.PlayerEntity.LocationRoomId);
                room?.RemoveEntity(session.PlayerEntity);
            }

            _world.UntrackEntity(session.PlayerEntity);

            _eventBus.Publish(new GameEvent
            {
                Type = "player.logout",
                SourceEntityId = evt.EntityId,
                Data = new Dictionary<string, object?> { ["reason"] = evt.Reason }
            });

            if (lastRoomId != null)
            {
                _sessions.SendToRoom(lastRoomId, playerName + " fades from existence.\r\n");
            }

            _logger.LogInformation("Player {Name} disconnected: {Reason}", playerName, evt.Reason);
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
