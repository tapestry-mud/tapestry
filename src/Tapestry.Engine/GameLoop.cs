using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Registration;
using Tapestry.Engine.Stats;
using Tapestry.Engine.Rest;
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
    private readonly NotificationQueue _notificationQueue;
    private readonly RegistrationPolicy _registrationPolicy;
    private readonly HashSet<Guid> _activeDisconnects = new();
    private readonly Dictionary<string, TickHandler> _handlers = new();
    private readonly SortedDictionary<long, List<string>> _dueSlots = new();
    private readonly Dictionary<string, long> _nextDue = new();
    private readonly ConcurrentQueue<Action> _pendingActions = new();
    private const int MaxCommandsPerSessionPerTick = 10;
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
    public event Action<SessionManager, NotificationQueue>? OnNotificationDrain;

    public GameLoop(CommandRouter router, SessionManager sessions, EventBus eventBus,
                    SystemEventQueue eventQueue, ILogger<GameLoop> logger,
                    TapestryMetrics metrics, TickTimer timer,
                    NotificationQueue notificationQueue,
                    RegistrationPolicy registrationPolicy)
    {
        _router = router;
        _sessions = sessions;
        _eventBus = eventBus;
        _eventQueue = eventQueue;
        _logger = logger;
        _metrics = metrics;
        _timer = timer;
        _notificationQueue = notificationQueue;
        _registrationPolicy = registrationPolicy;
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

    public void RegisterTickHandler(string name, int intervalTicks, Action handler, string packName = "kernel")
    {
        if (intervalTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalTicks), "Interval must be greater than zero.");
        }

        // Declarative: accumulate a candidate; the real insert runs at Resolve() (seal), so `due`
        // is computed against the correct _tickCount and arrival order never decides the winner.
        // A duplicate name now collides at the seal (RegistrationPolicy), not eagerly here.
        _registrationPolicy.Record(new RegistrationCandidate(
            Kind: "tick",
            Name: name,
            Owner: packName,
            IsOverride: false,        // tick handlers are kernel-only today; no JS override path (see Plan 2)
            Commit: () =>
            {
                var due = _tickCount + intervalTicks;
                _handlers[name] = new TickHandler(name, intervalTicks, packName, handler);
                _nextDue[name] = due;
                if (!_dueSlots.ContainsKey(due))
                {
                    _dueSlots[due] = new List<string>();
                }
                _dueSlots[due].Add(name);
            },
            SourceFile: "",
            Line: 0));
    }

    public void CancelTickHandler(string name)
    {
        // Pre-seal, a name may live only in the ledger (not yet in _handlers). Pull the candidate
        // from the ledger rather than touching the (still-empty) dispatch dicts.
        if (!_registrationPolicy.IsSealed)
        {
            _registrationPolicy.Remove("tick", name);
            return;
        }

        if (!_handlers.ContainsKey(name)) { return; }
        _handlers.Remove(name);
        if (_nextDue.TryGetValue(name, out var due))
        {
            _nextDue.Remove(name);
            if (_dueSlots.TryGetValue(due, out var slot))
            {
                slot.Remove(name);
                if (slot.Count == 0) { _dueSlots.Remove(due); }
            }
        }
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

        // 1. Drain scheduled actions (posted from network threads to run on game loop thread)
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

        // 2. Process system events
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

        // 3. Process incoming commands
        var cmdSw = Stopwatch.StartNew();
        var commandsProcessed = 0;
        Activity? cmdActivity = TapestryTracing.Source.StartActivity("ProcessCommands");
        foreach (var session in _sessions.AllSessions)
        {
            _metrics.InputQueueDepth.Record(session.InputQueueCount);

            var sessionCommandsThisTick = 0;
            while (session.TryDequeueInput(out var input))
            {
                if (sessionCommandsThisTick >= MaxCommandsPerSessionPerTick) { break; }
                sessionCommandsThisTick++;

                session.UpdateLastInputTick(_tickCount);

                if (string.IsNullOrWhiteSpace(input))
                {
                    session.NeedsPromptRefresh = true;
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

                // Parse (chaining/repeat/alias expansion) lives on the router so that
                // admin.executeAs dispatches through the exact same path as queued input.
                var contexts = _router.ParseInput(session.PlayerEntity.Id, actualInput,
                    session.Phase == LoginPhase.Creating);
                foreach (var ctx in contexts)
                {
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
        }
        cmdSw.Stop();
        cmdActivity?.Dispose();
        _metrics.TickDuration.Record(cmdSw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("phase", "commands"));
        _metrics.CommandsProcessed.Add(commandsProcessed);

        // 4. Drain notifications (deferred player messages: quest banners, achievements, etc.)
        var notifySw = Stopwatch.StartNew();
        Activity? notifyActivity = TapestryTracing.Source.StartActivity("DrainNotifications");
        OnNotificationDrain?.Invoke(_sessions, _notificationQueue);
        notifySw.Stop();
        notifyActivity?.Dispose();
        _metrics.TickDuration.Record(notifySw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("phase", "notifications"));

        // 5. Run tick handlers (due-ordered dispatch)
        var handlersSw = Stopwatch.StartNew();
        Activity? handlersActivity = TapestryTracing.Source.StartActivity("TickHandlers");
        while (_dueSlots.Count > 0 && _dueSlots.Keys.First() <= _tickCount)
        {
            var dueKey = _dueSlots.Keys.First();
            var names = _dueSlots[dueKey].ToList();
            _dueSlots.Remove(dueKey);

            foreach (var handlerName in names)
            {
                if (!_handlers.TryGetValue(handlerName, out var handler)) { continue; }

                // Reschedule before invoking (so cancels inside the handler work cleanly)
                var nextDue = dueKey + handler.IntervalTicks;
                _nextDue[handlerName] = nextDue;
                if (!_dueSlots.ContainsKey(nextDue))
                {
                    _dueSlots[nextDue] = new List<string>();
                }
                _dueSlots[nextDue].Add(handlerName);

                using var handlerSpan = TapestryTracing.Source.StartActivity($"TickHandler.{handlerName}");
                handlerSpan?.SetTag("handler.name", handlerName);
                handlerSpan?.SetTag("handler.pack", handler.PackName);
                handlerSpan?.SetTag("handler.interval", handler.IntervalTicks);
                var handlerCpuStart = ThreadCpuClock.GetCurrentThreadCpuTime();
                var handlerTimerSw = Stopwatch.StartNew();
                try
                {
                    handler.Action();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tick handler error: handler={HandlerName} pack={PackName}", handlerName, handler.PackName);
                }
                handlerTimerSw.Stop();
                var handlerMs = handlerTimerSw.Elapsed.TotalMilliseconds;
                var handlerCpuMs = Math.Max(0.0,
                    (ThreadCpuClock.GetCurrentThreadCpuTime() - handlerCpuStart).TotalMilliseconds);
                handlerSpan?.SetTag("handler.duration_ms", handlerMs);
                handlerSpan?.SetTag("handler.cpu_ms", handlerCpuMs);

                var handlerTag = new KeyValuePair<string, object?>("handler", handlerName);
                _metrics.HandlerWallMs.Record(handlerMs, handlerTag);
                _metrics.HandlerCpuMs.Record(handlerCpuMs, handlerTag);

                if (_slowTickThresholdMs > 0 && handlerMs > _slowTickThresholdMs)
                {
                    var cpuClass = HandlerCpuClassifier.Classify(handlerMs, handlerCpuMs, ThreadCpuClock.IsSupported);
                    _logger.LogWarning(
                        "Slow tick handler: {Handler} (pack={Pack}) took {Wall:F1}ms wall / {Cpu:F1}ms cpu ({Class}) (budget: {Budget}ms)",
                        handlerName, handler.PackName, handlerMs, handlerCpuMs, cpuClass, _slowTickThresholdMs);
                }
            }
        }
        handlersSw.Stop();
        handlersActivity?.Dispose();
        _metrics.TickDuration.Record(handlersSw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("phase", "handlers"));

        // 6. Flush prompts for any session that received output this tick
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
                "Slow tick {TickNumber}: {DurationMs:F1}ms (budget: {Budget}ms) -- pre_tick: {PreTickMs:F1}ms, scheduled: {ScheduledMs:F1}ms, events: {EventMs:F1}ms, commands: {CmdMs:F1}ms, notifications: {NotifyMs:F1}ms, handlers: {HandlerMs:F1}ms, flush: {FlushMs:F1}ms",
                _tickCount, totalMs, _slowTickThresholdMs,
                preTickSw.Elapsed.TotalMilliseconds, scheduledSw.Elapsed.TotalMilliseconds,
                eventSw.Elapsed.TotalMilliseconds, cmdSw.Elapsed.TotalMilliseconds,
                notifySw.Elapsed.TotalMilliseconds, handlersSw.Elapsed.TotalMilliseconds,
                flushSw.Elapsed.TotalMilliseconds);

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
                if (!session.Connection.IsConnected)
                {
                    eventQueue.Enqueue(new DisconnectEvent(
                        Guid.Parse(session.Connection.Id),
                        session.PlayerEntity.Id,
                        "dead connection"));
                    continue;
                }

                // AFK kick applies only to in-world (Playing) sessions -- never mid-chargen/login.
                if (session.Phase != LoginPhase.Playing) { continue; }

                if (session.PlayerEntity.HasRole(adminTag)) { continue; }

                // Drive the AFK decision purely off input recency, NOT the stale IsConnected flag
                // (a half-open connection reports Connected=true; heartbeat detection handles that).
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

    /// <summary>
    /// Registers a periodic liveness sweep: every <paramref name="intervalTicks"/> ticks it
    /// calls <see cref="IConnection.Heartbeat"/> on each PLAYING session's connection. The
    /// heartbeat is a tiny invisible write whose only job is to provoke a TCP send; against a
    /// half-open peer the write errors and the connection tears itself down through the normal
    /// Disconnect -> OnDisconnected -> link-dead -> timeout flow. No cleanup happens here.
    /// Registered as "connection-heartbeat" -- NOT "heartbeat", which is the combat pulse
    /// handler (HeartbeatManager.Tick); reusing that name silently clobbers the attack loop.
    /// </summary>
    public void RegisterHeartbeatHandler(SessionManager sessions, int intervalTicks)
    {
        RegisterTickHandler("connection-heartbeat", intervalTicks, () =>
        {
            foreach (var session in sessions.AllSessions)
            {
                if (session.Phase != LoginPhase.Playing) { continue; }
                session.Connection.Heartbeat();
            }
        });
    }

    public void RegisterRegenHandler(World world, EventBus eventBus, int regenIntervalTicks = 10, RestConfig? restConfig = null)
    {
        var regenData = new Dictionary<string, object?>(2);
        RegisterTickHandler("regen", regenIntervalTicks, () =>
        {
            var regenCandidates = world.GetEntitiesByType("player")
                .Concat(world.GetEntitiesByType("npc"))
                .Where(e => !e.HasTag("no_regen"));
            foreach (var entity in regenCandidates)
            {
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

                var finalMultiplier = restMultiplier;
                if (finalMultiplier == 0.0) { continue; }

                var regenHp = (int)Math.Round(entity.GetProperty<int>(CommonProperties.RegenHp) * finalMultiplier);
                var regenResource = (int)Math.Round(entity.GetProperty<int>(CommonProperties.RegenResource) * finalMultiplier);
                var regenMovement = (int)Math.Round(entity.GetProperty<int>(CommonProperties.RegenMovement) * finalMultiplier);

                if (regenHp > 0 && entity.Stats.Hp < entity.Stats.MaxHp)
                {
                    regenData.Clear();
                    regenData["vital"] = "hp";
                    regenData["amount"] = regenHp;
                    var regenEvent = new GameEvent
                    {
                        Type = "entity.regen",
                        SourceEntityId = entity.Id,
                        RoomId = entity.LocationRoomId,
                        Data = regenData
                    };
                    eventBus.Publish(regenEvent);

                    if (!regenEvent.Cancelled)
                    {
                        var amount = regenData.ContainsKey("amount") ? Convert.ToInt32(regenData["amount"]) : regenHp;
                        entity.Stats.Hp += amount;
                    }
                }

                if (regenResource > 0 && entity.Stats.Resource < entity.Stats.MaxResource)
                {
                    regenData.Clear();
                    regenData["vital"] = "resource";
                    regenData["amount"] = regenResource;
                    var regenResourceEvent = new GameEvent
                    {
                        Type = "entity.regen",
                        SourceEntityId = entity.Id,
                        RoomId = entity.LocationRoomId,
                        Data = regenData
                    };
                    eventBus.Publish(regenResourceEvent);

                    if (!regenResourceEvent.Cancelled)
                    {
                        var amount = regenData.ContainsKey("amount") ? Convert.ToInt32(regenData["amount"]) : regenResource;
                        entity.Stats.Resource += amount;
                    }
                }

                if (regenMovement > 0 && entity.Stats.Movement < entity.Stats.MaxMovement)
                {
                    regenData.Clear();
                    regenData["vital"] = "movement";
                    regenData["amount"] = regenMovement;
                    var regenMovementEvent = new GameEvent
                    {
                        Type = "entity.regen",
                        SourceEntityId = entity.Id,
                        RoomId = entity.LocationRoomId,
                        Data = regenData
                    };
                    eventBus.Publish(regenMovementEvent);

                    if (!regenMovementEvent.Cancelled)
                    {
                        var amount = regenData.ContainsKey("amount") ? Convert.ToInt32(regenData["amount"]) : regenMovement;
                        entity.Stats.Movement += amount;
                    }
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

    private record TickHandler(string Name, int IntervalTicks, string PackName, Action Action);
}
