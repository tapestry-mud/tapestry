---
capability: world-simulation
last-updated: 2026-06-12
---

# World Simulation

## Overview

Three systems give the world a sense of time and place: `GameClock` advances an in-game
24-hour clock and emits events at hour and period boundaries; `WeatherService` rolls
stochastic weather-state transitions for each weather zone and dispatches messages to
affected rooms; and `EmoteRegistry` lets packs register named emote templates that the
engine formats and delivers. All three are data-driven -- the engine defines no zone
definitions, emote names, or period messages of its own. Heartbeat timing that drives
`GameClock.Tick()` is out of scope (see heartbeat.md). Area resets are also out of scope
(heartbeat.md). Combat is out of scope (combat.md).

## Behavior

### GameClock -- in-game time model

- The clock counts a 24-hour day (hours 0-23). `CurrentHour`, `CurrentPeriod`, and
  `DayCount` are the only public state. Period is one of four named values:
  `Dawn`, `Day`, `Dusk`, `Night` (enum `TimePeriod`).
  (src/Tapestry.Engine/GameClock.cs:6, :14-16)

- `GameClock.Tick()` is called every game-loop tick. A new in-game hour advances when
  `_tickCount % config.Game.TicksPerGameHour == 0`. The default is 600 ticks per
  game-hour; at 100 ms/tick that is one game-hour per real minute.
  (GameClock.cs:28; src/Tapestry.Data/ServerConfig.cs:274)

- When `CurrentHour` exceeds 23 it wraps to 0 and `DayCount` increments.
  (GameClock.cs:31-34)

- Period boundaries are configured in `server.yaml` under `game.period_boundaries` as
  four hour values `[dawn, day, dusk, night]`. Defaults are `[5, 8, 18, 20]`.
  (ServerConfig.cs:275; GameClock.cs:68-74)

- When the period changes on a new hour a `time.period.change` event is published first,
  carrying `period`, `previousPeriod`, and `hour`. Then `time.hour.change` is always
  published, carrying `hour`, `period`, and `dayCount`. If the period did not change,
  only `time.hour.change` fires.
  (GameClock.cs:40-63)

- The `time` JS namespace exposes four read-only accessors: `time.hour()`,
  `time.period()`, `time.dayCount()`, and `time.ticksPerHour()`. Scripts cannot
  advance or set the clock directly.
  (src/Tapestry.Scripting/Modules/TimeModule.cs:24-28)

This spec owns game-clock semantics. The heartbeat.md spec covers only the tick
dispatch mechanics that drive the clock; it does not duplicate clock behavior.

### Weather zones -- definition and registration

- A `WeatherZoneDefinition` has an `Id`, a `States` list, a `Transitions` map, a
  `TerrainMessages` map, and a `TerrainTransitions` map.
  (src/Tapestry.Engine/WeatherZoneDefinition.cs:1-10)

- `Transitions` is `Dictionary<string, Dictionary<string, int>>`: for each current state
  the inner map gives integer weights for every possible next state. Weights are summed
  and a random roll selects the next state; a weight of 0 makes a transition impossible.
  (WeatherService.cs:124-137; WeatherServiceTests.cs:24-29)

- `TerrainMessages` is `Dictionary<terrain, Dictionary<state, WeatherMessages>>`.
  `WeatherMessages` carries `Start`, `Ongoing`, and `End` string fields. Only `Start`
  and `End` are used by `SendWeatherMessages`; `Ongoing` is stored but not dispatched
  automatically. (WeatherService.cs:146-151; WeatherZoneDefinition.cs:8)

- `TerrainTransitions` is `Dictionary<terrain, Dictionary<period, string>>`: a flat
  string message per period per terrain, sent on `time.period.change`.
  (WeatherZoneDefinition.cs:9; WeatherServiceTests.cs:38-47)

- Zones are held in `WeatherZoneRegistry` (case-insensitive key). Packs register zones
  by calling `WeatherZoneRegistry.Register(def)` from a script or YAML loader. The
  shipped `@tapestry/core` pack registers a `temperate` zone with states
  `[clear, cloudy, rain, storm]` and terrain messages for `forest`, `city`, and `road`.
  (src/Tapestry.Engine/WeatherZoneRegistry.cs; packs/@tapestry/core/areas/weather_zones.yaml:1-71)

- An area opts in to weather by setting `WeatherZone` to a zone id in its area
  definition. Areas with no `WeatherZone` are silently skipped by the roll loop.
  (WeatherService.cs:73-75)

### WeatherService -- state transitions and dispatch

- `WeatherService` subscribes to `time.hour.change` and `time.period.change` on the
  `EventBus` at construction time. (WeatherService.cs:28-29)

- On `time.hour.change`: if `hour % config.Game.WeatherRollIntervalHours != 0` the
  handler returns immediately. Default `WeatherRollIntervalHours` is 24, so weather
  rolls once per in-game day. (WeatherService.cs:65; ServerConfig.cs:278)

- When a roll fires, every registered area that has a weather zone receives a new state
  sampled from `RollTransition`. The roll runs for all zoned areas even if none are
  occupied; this keeps world state consistent. (WeatherService.cs:67-100)

- If the new state differs from the current state, `weather.change` is published on the
  event bus with `areaId`, `state`, and `previousState`. No event is emitted when the
  state does not change.
  (WeatherService.cs:85-94; WeatherServiceTests.cs:167-200)

- Weather messages are sent only to occupied rooms (rooms containing at least one logged-
  in player). Unoccupied rooms receive the state update but no text.
  (WeatherService.cs:45-60, :96-99; commit 7d769c3)

- On `time.period.change`: time-of-day messages are sent to all occupied rooms that pass
  `ShouldReceiveTimeTransition`. (WeatherService.cs:103-121)

- `ShouldReceiveWeather(room)` returns true when terrain is not `indoors` or
  `underground`, OR when `room.WeatherExposed == true` overrides the shield.
  `ShouldReceiveTimeTransition` follows the same logic using `room.TimeExposed`.
  (WeatherService.cs:159-171;
  WeatherServiceTests.cs:ShouldReceiveWeather_IndoorRoom_ReturnsFalse,
  WeatherServiceTests.cs:ShouldReceiveWeather_WeatherExposedIndoorRoom_ReturnsTrue)

- Message resolution uses a three-level priority chain: room overrides area, area
  overrides zone terrain. The first non-null result wins; null means no message.
  For weather: `room.WeatherMessages[state]` -> `area.WeatherMessages[state]` ->
  `zone.TerrainMessages[terrain][state]`. For time: `room.TimeMessages[period]` ->
  `area.TimeMessages[period]` -> `zone.TerrainTransitions[terrain][period]`.
  (WeatherService.cs:173-210;
  WeatherServiceTests.cs:MessageResolution_RoomOverridesArea,
  WeatherServiceTests.cs:MessageResolution_FallsBackToTerrain)

- `GetCurrentWeather(areaId)` returns the current state string, defaulting to `"clear"`
  for unknown areas. `SetWeather(areaId, state)` writes a state directly with no event
  and no message. (WeatherService.cs:32-40;
  WeatherServiceTests.cs:GetCurrentWeather_UnknownArea_ReturnsClear)

- The `weather` JS namespace exposes two functions: `weather.current(areaId)` and
  `weather.set(areaId, state)`. (src/Tapestry.Scripting/Modules/WeatherModule.cs:21-22)

### Emote registry -- registration and formatting

- `EmoteDefinition` has `Name`, `SelfMessage`, `RoomMessage`, and optional
  `TargetMessage` / `TargetRoomMessage`. `EmoteDefinition` defines a `TargetRoomMessage`
  field (EmoteRegistry.cs:9) for a third-party-observer message, but it is never
  populated during registration and is never dispatched; emotes have no third-party
  observer message.
  (src/Tapestry.Engine/EmoteRegistry.cs:1-10; EmotesModule.cs:61-67)

- `EmoteRegistry.Register(emote)` is an upsert gated by a `RegistrationGate`. Writes
  that arrive outside the seal scope raise an assertion error; collision resolution is
  handled upstream by `RegistrationPolicy` before the write reaches the registry.
  (EmoteRegistry.cs:26-29; see also registries-and-seal.md)

- Pack scripts register emotes via `emotes.register({ name, self, room, target?,
  override? })`. The `override` field must be a JS boolean `true` to set
  `IsOverride = true`; any other type (including `undefined`) is treated as false.
  (EmotesModule.cs:27-68)

- `EmoteRegistry.Format(template, actorName, targetName?)` replaces `{name}` with the
  actor's name, `{possessive}` with `actorName + "'s"`, and `{target}` with the target's
  name (only when `targetName` is non-null). (EmoteRegistry.cs:39-53)

- `EmoteRegistry.AllEmotes` enumerates all registered emote names (case-insensitive
  store; keys are as registered). (EmoteRegistry.cs:37)

## Rejected and Reverted

No tombstones on file. The occupied-room optimization in `WeatherService` (commit
7d769c3) replaced a prior O(areas x all-rooms) scan; the old scan is documented in a
code comment but there is no evidence of a separate spec or shipped behavior that was
formally reverted -- this is an implementation change, not a reversal of game behavior.

## Change Log

| Date | Change Record | Summary |
|------|---------------|---------|
