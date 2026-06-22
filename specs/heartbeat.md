---
capability: heartbeat
last-updated: 2026-06-22
---

# Heartbeat / Tick System

## Overview

The engine runs a single-threaded game loop that advances on a fixed wall-clock interval
(default 100 ms, configured via `server.tick_rate_ms` in `server.yaml`). Every iteration
of the loop is one "tick". Two overlapping subsystems share the tick counter: the
`GameLoop` (command processing, event draining, registered tick handlers) and the
`HeartbeatManager` (IPulseHandler registry for high-frequency sub-tick work such as
combat). Area-level ticks are a third layer: each area owns its own slower interval,
optionally scaled by player occupancy.

Combat resolution is handled inside the `CombatPulse` IPulseHandler; that logic is out of
scope for this spec and is covered in combat-resolution.md.

Game-clock semantics (time periods, in-game hour/period events) are owned by
world-simulation.md; this spec covers only the tick dispatch mechanics.

Regen events (entity.regen, RestService multipliers) are documented in rest-and-recovery.md.

## Behavior

### Game loop phases

Each call to `GameLoop.Tick()` executes the following phases in order.
(src/Tapestry.Engine/GameLoop.cs)

- **PreTick:** An optional `Action` installed by `SetPreTickAction`. In the shipped server
  wiring this swaps the world's tag double-buffers (`World.SwapTagBuffers`).
  (src/Tapestry.Server/Modules/TickHandlerModule.cs:71)

- **ScheduledActions:** Drains a `ConcurrentQueue<Action>` of lambdas posted from network
  threads (via `GameLoop.Schedule(Action)`). This is the only safe cross-thread injection
  point into the game loop. (GameLoop.cs:61, GameLoop.cs:155)

- **ProcessEvents:** Drains the `SystemEventQueue`. Disconnect events are deduplicated
  within one tick by entity ID; the first wins. Each event raises `OnSystemEventProcessed`;
  `DisconnectEvent` additionally raises `OnDisconnect`.
  (GameLoop.cs:173; GameLoopHardeningTests.Tick_processes_system_events_before_commands)

- **ProcessCommands:** For every active session, up to 10 commands per session per tick are
  dequeued and routed through `CommandRouter.Route`. Empty input sets
  `NeedsPromptRefresh = true` without routing. Input `!` repeats the last command.
  (GameLoop.cs:208; GameLoop.cs:28 `MaxCommandsPerSessionPerTick`)

- **DrainNotifications:** Fires `OnNotificationDrain` so subscribers can deliver queued
  player messages (GMCP banners, achievements, etc.) after all commands have run.
  (GameLoop.cs:282; GameLoopHardeningTests.Tick_drains_notifications_after_commands)

- **TickHandlers:** Dispatches all registered tick handlers whose due-slot is <= the current
  tick count, in ascending due-order. Each handler is rescheduled for its next due slot
  before its `Action` is invoked, so a cancel issued from inside a handler is safe.
  Exceptions are caught and logged; subsequent handlers still fire.
  (GameLoop.cs:290; GameLoopTests.Tick_FirstHandlerThrows_SecondHandlerStillFires)

- **FlushPrompts:** Fires `OnTickComplete`, which the server wires to
  `SessionManager.FlushPrompts`. (GameLoop.cs:349; GameLoopService.cs:295)

### Tick rate and timing

- The default tick rate is 100 ms (`tick_rate_ms: 100` in `server.yaml`). At this rate,
  10 ticks equal 1 second. (server.yaml:7; GameLoopService.cs:303)

- `GameLoop.RunAsync` drives the loop with `Task.Delay(tickRateMs, ct)` between ticks.
  Tick wall-time is not subtracted from the delay; a slow tick pushes the next tick back.
  (GameLoop.cs:590)

- `TickTimer` converts between ticks and real-time seconds using a `TicksPerSecond`
  constant set at construction. (src/Tapestry.Engine/TickTimer.cs)

- Slow-tick detection fires `OnSlowTick` and logs a warning when the entire tick exceeds
  `slow_tick_threshold_ms` (default 50 ms). The warning fires once per tick, covering the
  whole tick duration; per-handler overruns are logged separately but do not individually
  trigger `OnSlowTick`. Per-handler wall and CPU time are recorded as OpenTelemetry
  histogram metrics (`tapestry.tick.handler_wall_ms`, `tapestry.tick.handler_cpu_ms`) and
  the handler is classified as "cpu-bound" or "preempted" based on CPU vs wall ratio.
  (GameLoop.cs:366-377; GameLoopTests.Tick_CapturesHandlerCpu_DistinguishesSleepFromSpin)

### Tick handler registration

- `GameLoop.RegisterTickHandler(name, intervalTicks, action, packName)` registers a named
  periodic action. Interval must be >= 1; zero or negative throws
  `ArgumentOutOfRangeException`.
  (GameLoop.cs:76; GameLoopTests.RegisterTickHandler_WithIntervalZeroOrNegative_Throws)

- Registration is declarative: the candidate is recorded in `RegistrationPolicy` and does
  not take effect until `RegistrationPolicy.Resolve()` (the seal) commits it. First due
  slot is `tickCount + intervalTicks` at seal time.
  (GameLoop.cs:86; GameLoopTests.RegisterTickHandler_DueOrdered_FiresAtRegistrationPlusInterval)

- Duplicate names are a boot error raised at the seal. The canonical workaround is to
  cancel the first registration before registering the replacement.
  (GameLoop.cs:85 comment; GameLoopTests.RegisterTickHandler_SameName_Throws;
  GameLoopTests.RegisterTickHandler_CancelThenReRegister_ReplacesHandler)

- `CancelTickHandler(name)` removes a handler pre-seal (from the ledger) or post-seal
  (from the dispatch maps). A cancel issued inside a running handler is safe because
  rescheduling happens before invocation. (GameLoop.cs:106)

### Built-in tick handlers (registered by TickHandlerModule.Configure)

The following handlers are wired by `TickHandlerModule.Configure`.
(src/Tapestry.Server/Modules/TickHandlerModule.cs)

| Name | Interval | Purpose |
|------|----------|---------|
| area-tick | 1 tick | Drives `AreaTickService.Tick()` |
| game-clock | 1 tick | Advances `GameClock`; emits `time.hour.change` / `time.period.change` |
| tick-timer | 1 tick | Advances the `TickTimer` counter |
| mob-command-queue | 1 tick | Processes queued mob commands |
| heartbeat | 1 tick | Calls `HeartbeatManager.Tick()` (runs all IPulseHandlers) |
| gmcp-vitals-flush | 1 tick | Flushes dirty GMCP vitals to connected clients |
| flow-async-resume | 1 tick | Resumes any session flow awaiting an async result |
| mob-ai | 10 ticks | Runs `MobAIManager.Tick()` |
| regen | 30 ticks | Regenerates HP/resource/movement for players and NPCs |
| corpse-decay | 30 ticks | Checks and removes expired corpses |
| autosave | config | Snapshots and writes all player saves (`persistence.autosave_interval`) |

### Built-in tick handlers (registered by GameLoopService.Configure)

The following handlers are wired by `GameLoopService.Configure`.
(src/Tapestry.Server/GameLoopService.cs)

| Name | Interval | Purpose |
|------|----------|---------|
| idle-timeout | 300 ticks | AFK warning and kick sweep (conditional: only when `idle.warn_seconds` or `idle.timeout_seconds` > 0) (GameLoopService.cs:195) |
| connection-heartbeat | config | Writes a liveness byte to each PLAYING session to detect half-open TCP (conditional: only when `networking.keep_alive.enabled` and `heartbeat_seconds` > 0) (GameLoopService.cs:205) |
| linkdead-cleanup | 300 ticks | Reaps sessions that have been link-dead past the timeout (conditional: only when `link_dead.enabled`) (GameLoopService.cs:213) |
| watch-roster | config | Re-pushes the watchable-player roster to anonymous spectators (conditional: only when `watch.enabled`) (GameLoopService.cs:274) |

### HeartbeatManager and IPulseHandler

`HeartbeatManager` is a secondary dispatcher that runs inside the "heartbeat" tick handler
(interval 1 tick, so it runs every tick). It maintains its own ordered list of
`IPulseHandler` implementations.

- `IPulseHandler` exposes three properties: `Name` (string), `Cadence` (int ticks between
  firings relative to the manager's own tick count), and `Priority` (int, lower fires
  first). (src/Tapestry.Engine/Heartbeat/IPulseHandler.cs)

- `HeartbeatManager.Register(IPulseHandler)` adds a handler. Handlers are sorted by
  `Priority` on the next tick after any registration (lazy dirty flag).
  (src/Tapestry.Engine/Heartbeat/HeartbeatManager.cs:66)

- On each `HeartbeatManager.Tick()`, the manager increments its own `_tickCount` and fires
  every handler whose `_tickCount % handler.Cadence == 0`.
  (HeartbeatManager.cs:87)

- Each handler receives a `PulseContext` that carries references to: `World`, `EventBus`,
  `CombatManager`, `AbilityRegistry`, `ProficiencyManager`, `PassiveAbilityProcessor`,
  `EffectManager`, `SessionManager`, `AlignmentManager`, `Random`, plus `CurrentTick` and
  `CurrentPulse` (the handler's own pulse index = `tickCount / cadence`).
  (src/Tapestry.Engine/Heartbeat/PulseContext.cs)

- `CombatPulse` is a registered IPulseHandler: `Name = "CombatPulse"`,
  `Cadence = 20`, `Priority = 100`. At 100 ms/tick this fires every 2 seconds.
  `CombatPulse` first runs `AbilityResolutionPhase`, then each `ICombatPhase` in
  priority order. (src/Tapestry.Engine/Heartbeat/CombatPulse.cs)

- `SwellClockPulse` is a registered IPulseHandler: `Cadence = 1` (every tick),
  `Priority = 90` (ahead of `CombatPulse`). Each tick it advances the per-fight
  swell clock for every swell boss in combat; the swell state machine emits its
  own variable beat gaps rather than rescheduling the handler.
  (src/Tapestry.Engine/Heartbeat/SwellClockPulse.cs:9;
  src/Tapestry.Engine/Heartbeat/SwellClockPulse.cs:24)

- NOTE: the name "heartbeat" also appears as the `GameLoop` tick handler name for
  `HeartbeatManager.Tick()`. A separate "connection-heartbeat" handler sends TCP keepalive
  writes. The code comment in `GameLoop.RegisterHeartbeatHandler` explicitly warns against
  reusing the name "heartbeat" to avoid silently clobbering the combat pulse loop.
  (GameLoop.cs:430 comment)

### Area ticks

`AreaTickService` is called every game-loop tick via the "area-tick" handler, but it fires
a `area.tick` event to the event bus for each area only when that area's internal counter
has reached its effective interval.

- Each area has a `ResetInterval` (in ticks) from its area definition. If at least one
  player is present the interval is multiplied by `OccupiedModifier` (a float from the area
  definition, typically > 1 to slow resets when occupied).
  (src/Tapestry.Engine/AreaTickService.cs:39)

- Per-area overrides of both `ResetInterval` and `OccupiedModifier` can be set at runtime
  via `SetResetInterval` and `SetOccupiedModifier`, which write to an `AreaTickState`
  record. (AreaTickService.cs:97)

- When an area fires, a `GameEvent` of type `"area.tick"` is published with `areaId`,
  `tickCount` (cumulative fires for this area), and `playerCount`.
  (AreaTickService.cs:48)

- Player counts are rebuilt from scratch each call to `AreaTickService.Tick()` by scanning
  all room entities. (AreaTickService.cs:75)

- An area whose effective reset interval is `<= 0` never fires `area.tick` after boot; it
  is populated once at startup via the boot path (which calls `RunAreaReset` directly) and
  then never repops. This is the repop-off guard used by solo/oracle areas.
  (AreaTickService.cs:38; RepopOffGuardTests)

### Game clock

`GameClock.Tick()` is called every game-loop tick. A new in-game hour advances when
`tickCount % config.Game.TicksPerGameHour == 0`. Day/night period boundaries (dawn, day,
dusk, night) are configurable via the `game.period_boundaries` key in `server.yaml`
(defaults: [5,8,18,20]); the shipped server.yaml omits the key so defaults always apply.
When the period changes, a `time.period.change` event is published; every hour change also
emits `time.hour.change`. (src/Tapestry.Engine/GameClock.cs)

### Pack JS scheduling API

Packs can schedule periodic work from JavaScript via the `schedule` namespace
(`src/Tapestry.Scripting/Modules/ScheduleModule.cs`):

- `schedule.every(ticks, fn)` -- registers a tick handler that calls `fn` every `ticks`
  game-loop ticks. Returns a handle string.

- `schedule.everyForEach(ticks, selector, fn)` -- same, but resolves a set of entities
  matching `{ id, type, tag }` each firing and calls `fn` once per entity.

- `schedule.cancel(handle)` -- cancels a previously registered handler.

Handler names are auto-generated as `<packName>:sched:<n>`. All handlers registered by a
pack are tracked; `ScheduleModule.ResetPack` cancels them all (used during pack reload).
Packs may not override each other's tick handlers -- tick handlers are kernel-only; no JS
override path exists. (GameLoop.cs:90 comment)

The `time` JS namespace (`src/Tapestry.Scripting/Modules/TimeModule.cs`) exposes
`time.hour()`, `time.period()`, `time.dayCount()`, and `time.ticksPerHour()` for
reading game clock state from scripts.

## Rejected and Reverted

- None on record.

## Change Log

- 2026-06-22 [solo-oracle-slice-1](changes/2026-06-22-solo-oracle-slice-1.md) - repop-off guard: `reset_interval <= 0` disables recurring area reset (solo/oracle areas populate once at boot, never repop)
- 2026-06-21 [boss-combat-slice-1](changes/2026-06-21-boss-combat-slice-1.md) - `SwellClockPulse` (Cadence 1, Priority 90) drives the per-fight swell clock
