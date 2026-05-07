using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Stats;
using Tapestry.Engine.Rest;
using Tapestry.Engine.Sustenance;
using Tapestry.Shared;

namespace Tapestry.Engine;

public class GameLoop
{
    private readonly CommandRouter _router;
    private readonly SessionManager _sessions;
    private readonly EventBus _eventBus;
    private readonly SystemEventQueue _eventQueue;
    private readonly ILogger<GameLoop> _logger;
    private readonly TapestryMetrics _metrics;
    private readonly TickTimer _timer;
    private readonly HashSet<Guid> _activeDisconnects = new();
    private readonly List<TickHandler> _tickHandlers = new();
    private readonly ConcurrentQueue<Action> _pendingActions = new();
    private long _tickCount;
    private Action? _preTick;
    private int _idleTimeoutTicks;
    private int _idleWarningTicks;
    private double _slowTickThresholdMs;

    public long TickCount => _tickCount;

    public event Action<SystemEvent>? OnSystemEventProcessed;
    public event Action<DisconnectEvent>? OnDisconnect;
    public event Action<long, double, double, double, double>? OnSlowTick;
    public event Action<CommandContext>? OnCommandProcessed;
    public event Action? OnTickComplete;

    public GameLoop(CommandRouter router, SessionManager sessions, EventBus eventBus,
                    SystemEventQueue eventQueue, ILogger<GameLoop> logger,
                    TapestryMetrics metrics, TickTimer timer)
    {
        _router = router;
        _sessions = sessions;
        _eventBus = eventBus;
        _eventQueue = eventQueue;
        _logger = logger;
        _metrics = metrics;
        _timer = timer;
    }

    public void Schedule(Action action)
    {
        _pendingActions.Enqueue(action);
    }

    public void SetPreTickAction(Action action)
    {
        _preTick = action;
    }

    public void ConfigureSlowTickThreshold(double thresholdMs)
    {
        _slowTickThresholdMs = thresholdMs;
    }

    public void RegisterTickHandler(string name, int intervalTicks, Action handler)
    {
        _tickHandlers.Add(new TickHandler(name, intervalTicks, handler));
    }

    public void ConfigureIdleTimeout(int warnSeconds, int timeoutSeconds)
    {
        _idleWarningTicks = warnSeconds > 0 ? (int)_timer.SecondsToTicks(warnSeconds) : 0;
        _idleTimeoutTicks = timeoutSeconds > 0 ? (int)_timer.SecondsToTicks(timeoutSeconds) : 0;
    }

    public void Tick()
    {
        _tickCount++;
        using var tickActivity = TapestryTracing.Source.StartActivity("GameLoop.Tick");
        tickActivity?.SetTag("tick.number", _tickCount);
        var tickSw = Stopwatch.StartNew();

        // PreTick (e.g. SwapTagBuffers) -- runs before all phases; measured separately
        var preTickSw = Stopwatch.StartNew();
        using (TapestryTracing.Source.StartActivity("PreTick"))
        {
            _preTick?.Invoke();
        }
        preTickSw.Stop();
        _metrics.TickDuration.Record(preTickSw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("phase", "pre_tick"));

        // 0. Drain scheduled actions (posted from network threads to run on game loop thread)
        var scheduledSw = Stopwatch.StartNew();
        var scheduledCount = 0;
        Activity? scheduledActivity = TapestryTracing.Source.StartActivity("ScheduledActions");
        while (_pendingActions.TryDequeue(out var pending))
        {
            try
            {
                pending();
                scheduledCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing scheduled game loop action");
            }
        }
        scheduledSw.Stop();
        scheduledActivity?.SetTag("scheduled.count", scheduledCount);
        scheduledActivity?.SetTag("scheduled.duration_ms", scheduledSw.Elapsed.TotalMilliseconds);
        scheduledActivity?.Dispose();

        // 1. Process system events
        var eventSw = Stopwatch.StartNew();
        var eventsProcessed = 0;
        Activity? eventActivity = TapestryTracing.Source.StartActivity("ProcessEvents");
        foreach (var evt in _eventQueue.DrainAll())
        {
            using var evtActivity = TapestryTracing.Source.StartActivity($"SystemEvent.{evt.GetType().Name}");
            evtActivity?.SetTag("system_event.type", evt.GetType().Name);

            if (evt is DisconnectEvent disconnectEvt)
            {
                if (_activeDisconnects.Contains(disconnectEvt.EntityId))
                {
                    continue;
                }
                _activeDisconnects.Add(disconnectEvt.EntityId);
                evtActivity?.SetTag("system_event.entity_id", disconnectEvt.EntityId.ToString());
                evtActivity?.SetTag("system_event.reason", disconnectEvt.Reason);

                var disconnectSw = Stopwatch.StartNew();
                OnDisconnect?.Invoke(disconnectEvt);
                disconnectSw.Stop();
                evtActivity?.SetTag("system_event.disconnect_ms", disconnectSw.Elapsed.TotalMilliseconds);
            }

            eventsProcessed++;
            OnSystemEventProcessed?.Invoke(evt);
        }
        _activeDisconnects.Clear();
        eventSw.Stop();
        eventActivity?.SetTag("events.count", eventsProcessed);
        eventActivity?.Dispose();
        _metrics.TickDuration.Record(eventSw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("phase", "events"));
        _metrics.EventsProcessed.Add(eventsProcessed);

        // 2. Process incoming commands
        var cmdSw = Stopwatch.StartNew();
        var commandsProcessed = 0;
        Activity? cmdActivity = TapestryTracing.Source.StartActivity("ProcessCommands");
        foreach (var session in _sessions.AllSessions)
        {
            _metrics.InputQueueDepth.Record(session.InputQueueCount);

            while (session.TryDequeueInput(out var input))
            {
                session.UpdateLastInputTick(_tickCount);

                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                string actualInput;
                if (input.Trim() == "!")
                {
                    if (session.LastCommand == null)
                    {
                        _sessions.SendToPlayer(session.PlayerEntity.Id, "Nothing to repeat.\r\n");
                        continue;
                    }
                    actualInput = session.LastCommand;
                    session.ReceivedInput = true;
                    _sessions.SendToPlayer(session.PlayerEntity.Id, actualInput + "\r\n");
                }
                else
                {
                    session.LastCommand = input;
                    actualInput = input;
                    session.ReceivedInput = true;
                }

                var parts = actualInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && parts[0].Length > 1 && !char.IsLetterOrDigit(parts[0][0]))
                {
                    var alias = parts[0][0].ToString();
                    if (_router.Resolve(alias) != null)
                    {
                        var remainder = parts[0][1..];
                        parts = new[] { alias, remainder }.Concat(parts[1..]).ToArray();
                    }
                }
                var ctx = new CommandContext
                {
                    PlayerEntityId = session.PlayerEntity.Id,
                    RawInput = actualInput,
                    Command = parts[0],
                    Args = parts.Length > 1 ? parts[1..] : [],
                    IsChargen = session.Phase == LoginPhase.Creating
                };

                var handlerSw = Stopwatch.StartNew();
                using var handlerActivity = TapestryTracing.Source.StartActivity($"Command.{ctx.Command}");
                handlerActivity?.SetTag("command.name", ctx.Command);
                handlerActivity?.SetTag("command.entity_id", ctx.PlayerEntityId.ToString());
                handlerActivity?.SetTag("command.raw_input", ctx.RawInput);
                try
                {
                    _router.Route(ctx);
                }
                catch (Exception ex)
                {
                    _sessions.SendToPlayer(ctx.PlayerEntityId, "An error occurred processing your command.\r\n");
                    _logger.LogError(ex, "Command error [{Command}] for entity {EntityId}", ctx.Command, ctx.PlayerEntityId);
                }
                OnCommandProcessed?.Invoke(ctx);
                handlerSw.Stop();
                handlerActivity?.SetTag("command.duration_ms", handlerSw.Elapsed.TotalMilliseconds);
                commandsProcessed++;
                _metrics.CommandDuration.Record(handlerSw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("command_name", ctx.Command));
            }
        }
        cmdSw.Stop();
        cmdActivity?.Dispose();
        _metrics.TickDuration.Record(cmdSw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("phase", "commands"));
        _metrics.CommandsProcessed.Add(commandsProcessed);

        // 3. Run tick handlers
        var handlersSw = Stopwatch.StartNew();
        Activity? handlersActivity = TapestryTracing.Source.StartActivity("TickHandlers");
        foreach (var handler in _tickHandlers)
        {
            if (_tickCount % handler.IntervalTicks == 0)
            {
                using var handlerSpan = TapestryTracing.Source.StartActivity($"TickHandler.{handler.Name}");
                handlerSpan?.SetTag("handler.name", handler.Name);
                handlerSpan?.SetTag("handler.interval", handler.IntervalTicks);
                var handlerTimerSw = Stopwatch.StartNew();
                try
                {
                    handler.Action();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tick handler error: handler={HandlerName}", handler.Name);
                }
                handlerTimerSw.Stop();
                handlerSpan?.SetTag("handler.duration_ms", handlerTimerSw.Elapsed.TotalMilliseconds);
            }
        }
        handlersSw.Stop();
        handlersActivity?.Dispose();
        _metrics.TickDuration.Record(handlersSw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("phase", "handlers"));

        // 4. Flush prompts for any session that received output this tick
        var flushSw = Stopwatch.StartNew();
        using (TapestryTracing.Source.StartActivity("FlushPrompts"))
        {
            OnTickComplete?.Invoke();
        }
        flushSw.Stop();

        // Log and broadcast slow ticks
        tickSw.Stop();
        var totalMs = tickSw.Elapsed.TotalMilliseconds;
        tickActivity?.SetTag("tick.duration_ms", totalMs);
        tickActivity?.SetTag("tick.commands_processed", commandsProcessed);
        tickActivity?.SetTag("tick.events_processed", eventsProcessed);
        _logger.LogDebug("Tick {TickNumber} completed in {DurationMs:F1}ms — commands: {Commands}, events: {Events}",
            _tickCount, totalMs, commandsProcessed, eventsProcessed);

        if (totalMs > _slowTickThresholdMs && _slowTickThresholdMs > 0)
        {
            _logger.LogWarning(
                "Slow tick {TickNumber}: {DurationMs:F1}ms (budget: {Budget}ms) -- pre_tick: {PreTickMs:F1}ms, scheduled: {ScheduledMs:F1}ms, events: {EventMs:F1}ms, commands: {CmdMs:F1}ms, handlers: {HandlerMs:F1}ms, flush: {FlushMs:F1}ms",
                _tickCount, totalMs, _slowTickThresholdMs,
                preTickSw.Elapsed.TotalMilliseconds, scheduledSw.Elapsed.TotalMilliseconds,
                eventSw.Elapsed.TotalMilliseconds, cmdSw.Elapsed.TotalMilliseconds,
                handlersSw.Elapsed.TotalMilliseconds, flushSw.Elapsed.TotalMilliseconds);

            OnSlowTick?.Invoke(_tickCount, totalMs, eventSw.Elapsed.TotalMilliseconds, cmdSw.Elapsed.TotalMilliseconds, handlersSw.Elapsed.TotalMilliseconds);
        }
    }

    public void RegisterIdleTimeoutHandler(SystemEventQueue eventQueue, SessionManager sessions,
        string warnMessage, string timeoutMessage, string adminTag = "admin")
    {
        // Check every 300 ticks (~30 seconds at 100ms tick rate)
        RegisterTickHandler("idle-timeout", 300, () =>
        {
            foreach (var session in sessions.AllSessions)
            {
                if (session.PlayerEntity.HasTag(adminTag)) { continue; }

                var idleTicks = _tickCount - session.LastInputTick;

                if (_idleTimeoutTicks > 0 && idleTicks >= _idleTimeoutTicks)
                {
                    eventQueue.Enqueue(new DisconnectEvent(
                        Guid.Parse(session.Connection.Id),
                        session.PlayerEntity.Id,
                        "idle timeout"));
                    session.Connection.SendLine(timeoutMessage);
                    session.Connection.Disconnect("idle timeout");
                }
                else if (_idleWarningTicks > 0 && idleTicks >= _idleWarningTicks && !session.IdleWarned)
                {
                    session.Connection.SendLine(warnMessage);
                    session.IdleWarned = true;
                }
            }
        });
    }

    public void RegisterRegenHandler(World world, EventBus eventBus, int regenIntervalTicks = 10, SustenanceConfig? sustenanceConfig = null, RestConfig? restConfig = null)
    {
        RegisterTickHandler("regen", regenIntervalTicks, () =>
        {
            foreach (var entity in world.GetEntitiesByTag("regen"))
            {
                var sustenanceMultiplier = 1.0;
                if (sustenanceConfig != null)
                {
                    var sustenance = entity.TryGetProperty<int>(SustenanceProperties.Sustenance, out var sustenanceVal)
                        ? sustenanceVal
                        : 100;
                    sustenanceMultiplier = sustenanceConfig.GetRegenMultiplier(sustenance);
                }

                var restMultiplier = 1.0;
                if (restConfig != null)
                {
                    var restState = entity.GetProperty<string?>(RestProperties.RestState) ?? RestProperties.StateAwake;
                    restMultiplier = restConfig.GetRestMultiplier(restState);

                    var restTargetStr = entity.GetProperty<string?>(RestProperties.RestTarget);
                    if (restTargetStr != null && Guid.TryParse(restTargetStr, out var furnitureId))
                    {
                        var furniture = world.GetEntity(furnitureId);
                        if (furniture != null)
                        {
                            restMultiplier += furniture.GetProperty<int>(RestProperties.RestBonus);
                        }
                    }

                    if (entity.LocationRoomId != null)
                    {
                        var room = world.GetRoom(entity.LocationRoomId);
                        if (room != null)
                        {
                            restMultiplier += room.GetProperty<int>(RestProperties.RoomHealingRate);
                        }
                    }
                }

                var finalMultiplier = sustenanceMultiplier * restMultiplier;
                if (finalMultiplier == 0.0) { continue; }

                var regenHp = (int)Math.Round(entity.GetProperty<int>(CommonProperties.RegenHp) * finalMultiplier);
                var regenResource = (int)Math.Round(entity.GetProperty<int>(CommonProperties.RegenResource) * finalMultiplier);
                var regenMovement = (int)Math.Round(entity.GetProperty<int>(CommonProperties.RegenMovement) * finalMultiplier);

                if (regenHp > 0 && entity.Stats.Hp < entity.Stats.MaxHp)
                {
                    var regenEvent = new GameEvent
                    {
                        Type = "entity.regen",
                        SourceEntityId = entity.Id,
                        RoomId = entity.LocationRoomId,
                        Data = { ["vital"] = "hp", ["amount"] = regenHp }
                    };
                    eventBus.Publish(regenEvent);

                    if (!regenEvent.Cancelled)
                    {
                        var amount = regenEvent.Data.ContainsKey("amount") ? (int)regenEvent.Data["amount"]! : regenHp;
                        entity.Stats.Hp += amount;
                    }
                }

                if (regenResource > 0 && entity.Stats.Resource < entity.Stats.MaxResource)
                {
                    var regenResourceEvent = new GameEvent
                    {
                        Type = "entity.regen",
                        SourceEntityId = entity.Id,
                        RoomId = entity.LocationRoomId,
                        Data = { ["vital"] = "resource", ["amount"] = regenResource }
                    };
                    eventBus.Publish(regenResourceEvent);

                    if (!regenResourceEvent.Cancelled)
                    {
                        var amount = regenResourceEvent.Data.ContainsKey("amount") ? (int)regenResourceEvent.Data["amount"]! : regenResource;
                        entity.Stats.Resource += amount;
                    }
                }

                if (regenMovement > 0 && entity.Stats.Movement < entity.Stats.MaxMovement)
                {
                    entity.Stats.Movement += regenMovement;
                }
            }
        });
    }

    public void CheckVitalDepletion(Entity entity, EventBus eventBus)
    {
        if (entity.Stats.MaxHp > 0 && entity.Stats.Hp <= 0)
        {
            eventBus.Publish(new GameEvent
            {
                Type = "entity.vital.depleted",
                SourceEntityId = entity.Id,
                RoomId = entity.LocationRoomId,
                Data = { ["vital"] = "hp" }
            });
        }

        if (entity.Stats.MaxResource > 0 && entity.Stats.Resource <= 0)
        {
            eventBus.Publish(new GameEvent
            {
                Type = "entity.vital.depleted",
                SourceEntityId = entity.Id,
                RoomId = entity.LocationRoomId,
                Data = { ["vital"] = "resource" }
            });
        }

        if (entity.Stats.MaxMovement > 0 && entity.Stats.Movement <= 0)
        {
            eventBus.Publish(new GameEvent
            {
                Type = "entity.vital.depleted",
                SourceEntityId = entity.Id,
                RoomId = entity.LocationRoomId,
                Data = { ["vital"] = "movement" }
            });
        }
    }

    public async Task RunAsync(int tickRateMs, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Tick();
            await Task.Delay(tickRateMs, ct).ConfigureAwait(false);
        }
    }

    private record TickHandler(string Name, int IntervalTicks, Action Action);
}
