using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Prompt;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Watch;
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
    private readonly WatchRegistry _watchRegistry;
    private readonly WatchSessionHub _watchSessionHub;
    private readonly IWatchRosterSource _watchRosterSource;
    private readonly Tapestry.Engine.Registration.RegistrationPolicy _registrationPolicy;
    private readonly Tapestry.Engine.Help.HelpSeal _helpSeal;
    private readonly SpawnManager _spawns;
    private readonly AbilityCommandBridge _abilityCommandBridge;
    private readonly Tapestry.Scripting.PackValidator _packValidator;
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
        Tapestry.Server.Gmcp.Handlers.NotificationHandler notificationHandler,
        WatchRegistry watchRegistry,
        WatchSessionHub watchSessionHub,
        IWatchRosterSource watchRosterSource,
        Tapestry.Engine.Registration.RegistrationPolicy registrationPolicy,
        Tapestry.Engine.Help.HelpSeal helpSeal,
        SpawnManager spawns,
        AbilityCommandBridge abilityCommandBridge,
        Tapestry.Scripting.PackValidator packValidator)
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
        _watchRegistry = watchRegistry;
        _watchSessionHub = watchSessionHub;
        _watchRosterSource = watchRosterSource;
        _registrationPolicy = registrationPolicy;
        _helpSeal = helpSeal;
        _spawns = spawns;
        _abilityCommandBridge = abilityCommandBridge;
        _packValidator = packValidator;

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

            // Clean up watch subscriptions: remove the player as both a watcher and a target.
            var entityId = session.PlayerEntity.Id;
            _watchRegistry.Unsubscribe(entityId.ToString());
            _watchRegistry.RemoveTarget(entityId);

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

        // Liveness heartbeat: provokes a write to each PLAYING connection so a half-open peer
        // (vanished without FIN/Close) errors out and routes into the existing link-dead flow.
        var heartbeatSeconds = _config.Networking.KeepAlive.HeartbeatSeconds;
        if (_config.Networking.KeepAlive.Enabled && heartbeatSeconds > 0)
        {
            var heartbeatTicks = Math.Max(1, (int)Math.Round(heartbeatSeconds * 1000.0 / _config.Server.TickRateMs));
            _gameLoop.RegisterHeartbeatHandler(_sessions, heartbeatTicks);
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

                    // Clean up watch subscriptions on final reap.
                    var linkDeadEntityId = session.PlayerEntity.Id;
                    _watchRegistry.Unsubscribe(linkDeadEntityId.ToString());
                    _watchRegistry.RemoveTarget(linkDeadEntityId);

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

        // Watch mode (Slice B): re-push the watchable-player roster to every anonymous spectator on
        // a low-frequency tick. SessionManager fires no add/remove events, so the roster is polled
        // here rather than pushed on change. No-op when nobody is watching.
        if (_config.Watch.Enabled)
        {
            var rosterTicks = Math.Max(1, _config.Watch.RosterIntervalTicks);
            _gameLoop.RegisterTickHandler("watch-roster", rosterTicks, () =>
            {
                if (_watchSessionHub.Count == 0) { return; }
                _watchSessionHub.Broadcast(_watchRosterSource.Snapshot());
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
        _registrationPolicy.Resolve();
        // Ability commands wire AFTER the seal: the ability registry only commits at
        // Resolve(). (Was: ContentLoadingModule.Configure, pre-seal — would see no abilities.)
        // Before HelpSeal so help shadow-validation/auto-gen sees the ability commands.
        _abilityCommandBridge.WireAll();
        // Pack validation AFTER the seal for the same reason: its mob ability/battlecommand
        // checks read the now-committed ability and command registries.
        _packValidator.Validate();
        // Help resolves AFTER commands (help shadows commands): validate command-shadowing
        // authority against the now-committed registry, then auto-gen fills only the gaps.
        _helpSeal.Seal();
        // Initial mob spawns run AFTER the seal: MobStatDerivation reads the class/race
        // registries, which only commit at Resolve(). (Was: PlayerInitModule.Configure,
        // which runs at bootstrap — pre-seal — and would see empty registries.)
        // GameLoopService starts before TelnetService, so the world is populated before
        // any player can connect.
        foreach (var areaName in _spawns.GetAreaNames())
        {
            _spawns.RunAreaReset(areaName);
        }
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
