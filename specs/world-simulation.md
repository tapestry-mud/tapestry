---
capability: world-simulation
last-updated: 2026-07-03
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
(heartbeat.md). Combat is out of scope (combat-resolution.md).

## Behavior

### GameClock -- in-game time model

- The clock counts a 24-hour day (hours 0-23). `CurrentHour`, `CurrentPeriod`, and
  `DayCount` are the only public state. Period is one of four named values:
  `Dawn`, `Day`, `Dusk`, `Night` (enum `TimePeriod`).
  (src/Tapestry.Engine/GameClock.cs:6, :14-16)

- `GameClock.Tick()` is called every game-loop tick. A new in-game hour advances when
  `_tickCount % config.Game.TicksPerGameHour == 0`. The default is 600 ticks per
  game-hour; at 100 ms/tick that is one game-hour per real minute.
  (src/Tapestry.Engine/GameClock.cs:28; src/Tapestry.Data/ServerConfig.cs:274)

- When `CurrentHour` exceeds 23 it wraps to 0 and `DayCount` increments.
  (src/Tapestry.Engine/GameClock.cs:31-34)

- Period boundaries are configured in `server.yaml` under `game.period_boundaries` as
  four hour values `[dawn, day, dusk, night]`. Defaults are `[5, 8, 18, 20]`.
  (src/Tapestry.Data/ServerConfig.cs:275; src/Tapestry.Engine/GameClock.cs:68-74)

- When the period changes on a new hour a `time.period.change` event is published first,
  carrying `period`, `previousPeriod`, and `hour`. Then `time.hour.change` is always
  published, carrying `hour`, `period`, and `dayCount`. If the period did not change,
  only `time.hour.change` fires.
  (src/Tapestry.Engine/GameClock.cs:40-63)

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
  (src/Tapestry.Engine/WeatherService.cs:124-137; tests/Tapestry.Engine.Tests/WeatherServiceTests.cs:24-29)

- `TerrainMessages` is `Dictionary<key, Dictionary<state, WeatherMessages>>`. The key is
  looked up twice per resolution -- once as a biome value, once as a terrain value (see
  "Message resolution" below) -- so the same map backs both lookups; a zone author keys an
  entry by whichever vocabulary it is meant to match. `WeatherMessages` carries `Start`,
  `Ongoing`, and `End` string fields. Only `Start` and `End` are used by
  `SendWeatherMessages`; `Ongoing` is stored but not dispatched automatically.
  (src/Tapestry.Engine/WeatherService.cs:149-162; src/Tapestry.Engine/WeatherZoneDefinition.cs:8)

- `TerrainTransitions` is `Dictionary<key, Dictionary<period, string>>`: a flat string
  message per period per key (biome or terrain, same dual lookup as `TerrainMessages`),
  sent on `time.period.change`.
  (src/Tapestry.Engine/WeatherZoneDefinition.cs:9; tests/Tapestry.Engine.Tests/WeatherServiceTests.cs:38-52)

- Zones are held in `WeatherZoneRegistry` (case-insensitive key). Packs register zones
  by calling `WeatherZoneRegistry.Register(def)` from a script or YAML loader. The
  shipped `@tapestry/core` pack registers a `temperate` zone with states
  `[clear, cloudy, rain, storm]` and a single `forest` terrain-message entry, matched
  biome-first against any room tagged with a biome-kind tag named `forest` (see
  `@tapestry/biomes`). There is no `city` or `road` entry, and no generic `outdoors`
  entry -- that flavor now lives on the individual rooms that carry it (room-level
  `weather_messages`/`time_messages`), not on the zone.
  (src/Tapestry.Engine/WeatherZoneRegistry.cs; packs/@tapestry/core/areas/weather_zones.yaml:1-30)

- An area opts in to weather by setting `WeatherZone` to a zone id in its area
  definition. Areas with no `WeatherZone` are silently skipped by the roll loop.
  (src/Tapestry.Engine/WeatherService.cs:73-75)

### WeatherService -- state transitions and dispatch

- `WeatherService` subscribes to `time.hour.change` and `time.period.change` on the
  `EventBus` at construction time. (src/Tapestry.Engine/WeatherService.cs:28-29)

- On `time.hour.change`: if `hour % config.Game.WeatherRollIntervalHours != 0` the
  handler returns immediately. Default `WeatherRollIntervalHours` is 24, so weather
  rolls once per in-game day. (src/Tapestry.Engine/WeatherService.cs:65; src/Tapestry.Data/ServerConfig.cs:278)

- When a roll fires, every registered area that has a weather zone receives a new state
  sampled from `RollTransition`. The roll runs for all zoned areas even if none are
  occupied; this keeps world state consistent. (src/Tapestry.Engine/WeatherService.cs:67-100)

- If the new state differs from the current state, `weather.change` is published on the
  event bus with `areaId`, `state`, and `previousState`. No event is emitted when the
  state does not change.
  (src/Tapestry.Engine/WeatherService.cs:95-104; tests/Tapestry.Engine.Tests/WeatherServiceTests.cs:167-200)

- Weather messages are sent only to occupied rooms (rooms containing at least one logged-
  in player). Unoccupied rooms receive the state update but no text.
  (src/Tapestry.Engine/WeatherService.cs:55-70,106-109; commit 7d769c3)

- On `time.period.change`: time-of-day messages are sent to all occupied rooms that pass
  `ShouldReceiveTimeTransition`. (src/Tapestry.Engine/WeatherService.cs:113-132)

- `ShouldReceiveWeather(room)` returns true when terrain is not `indoors` or
  `underground`, OR when `room.WeatherExposed == true` overrides the shield.
  `ShouldReceiveTimeTransition` follows the same logic using `room.TimeExposed`.
  (src/Tapestry.Engine/WeatherService.cs:169-181;
  tests/Tapestry.Engine.Tests/WeatherServiceTests.cs:ShouldReceiveWeather_IndoorRoom_ReturnsFalse,
  tests/Tapestry.Engine.Tests/WeatherServiceTests.cs:ShouldReceiveWeather_WeatherExposedIndoorRoom_ReturnsTrue)

- Message resolution uses a four-level priority chain: room overrides area, area
  overrides biome, biome overrides terrain. The first non-null result wins; null means
  no message. For weather: `room.WeatherMessages[state]` -> `area.WeatherMessages[state]`
  -> `zone.TerrainMessages[biome][state]` -> `zone.TerrainMessages[terrain][state]`. For
  time: `room.TimeMessages[period]` -> `area.TimeMessages[period]` ->
  `zone.TerrainTransitions[biome][period]` -> `zone.TerrainTransitions[terrain][period]`.
  The biome step is skipped (falls straight through to terrain) when the room carries no
  tag of kind `biome`; `terrain` always defaults to `"outdoors"` when the room declares
  none. This is a deliberate ruling: forest flavor rides the biome tag rather than the
  terrain value, so a room can be `terrain: outdoors` and still get forest-specific
  weather/time text via `biome: forest`.
  (src/Tapestry.Engine/WeatherService.cs:183-236;
  tests/Tapestry.Engine.Tests/WeatherServiceTests.cs:MessageResolution_RoomOverridesArea,
  tests/Tapestry.Engine.Tests/WeatherServiceTests.cs:MessageResolution_BiomeFirst_ForestBiomeWithOutdoorsTerrain_ReturnsForestMessage,
  tests/Tapestry.Engine.Tests/WeatherServiceTests.cs:MessageResolution_RoomLevelMessage_WinsOverBiome,
  tests/Tapestry.Engine.Tests/WeatherServiceTests.cs:MessageResolution_NoBiome_FallsBackToTerrain_IndoorsOnly,
  tests/Tapestry.Engine.Tests/WeatherServiceTests.cs:MessageResolution_FallsBackToTerrain)

- The biome value used in the chain above is recovered from the room's tag set: the
  engine has no dedicated "room biome" field at runtime (`biome:` in room YAML just adds
  a regular tag, see area-authoring.md), so `WeatherService.ResolveBiome` intersects
  `room.Tags` against every `TagRegistry` entry whose `Kind == "biome"`, matching on
  either the tag's bare `Name` or its scoped `FullName` (same pattern as
  `RoomProjector.Project` and `AreaMapProjector.CollectBiomeTagNames`). The biome-kind
  tag set is computed once and cached, since tag registration is complete before the
  service resolves its first message. `WeatherService` takes a `TagRegistry` constructor
  dependency for this lookup.
  (src/Tapestry.Engine/WeatherService.cs:243-261;
  src/Tapestry.Engine/Authoring/RoomProjector.cs:32-49;
  src/Tapestry.Engine/Mapping/AreaMapProjector.cs:157-169)

- `GetCurrentWeather(areaId)` returns the current state string, defaulting to `"clear"`
  for unknown areas. `SetWeather(areaId, state)` writes a state directly with no event
  and no message. (src/Tapestry.Engine/WeatherService.cs:32-40;
  tests/Tapestry.Engine.Tests/WeatherServiceTests.cs:GetCurrentWeather_UnknownArea_ReturnsClear)

- The `weather` JS namespace exposes two functions: `weather.current(areaId)` and
  `weather.set(areaId, state)`. (src/Tapestry.Scripting/Modules/WeatherModule.cs:21-22)

### Emote registry -- registration and formatting

- `EmoteDefinition` has `Name`, `SelfMessage`, `RoomMessage`, and optional
  `TargetMessage` / `TargetRoomMessage`. `EmoteDefinition` defines a `TargetRoomMessage`
  field (src/Tapestry.Engine/EmoteRegistry.cs:9) for a third-party-observer message, but it is never
  populated during registration and is never dispatched; emotes have no third-party
  observer message.
  (src/Tapestry.Engine/EmoteRegistry.cs:1-10; src/Tapestry.Scripting/Modules/EmotesModule.cs:61-67)

- `EmoteRegistry.Register(emote)` is an upsert gated by a `RegistrationGate`. Writes
  that arrive outside the seal scope raise an assertion error; collision resolution is
  handled upstream by `RegistrationPolicy` before the write reaches the registry.
  (src/Tapestry.Engine/EmoteRegistry.cs:26-29; see also registries-and-seal.md)

- Pack scripts register emotes via `emotes.register({ name, self, room, target?,
  override? })`. The `override` field must be a JS boolean `true` to set
  `IsOverride = true`; any other type (including `undefined`) is treated as false.
  (src/Tapestry.Scripting/Modules/EmotesModule.cs:27-68)

- `EmoteRegistry.Format(template, actorName, targetName?)` replaces `{name}` with the
  actor's name, `{possessive}` with `actorName + "'s"`, and `{target}` with the target's
  name (only when `targetName` is non-null). (src/Tapestry.Engine/EmoteRegistry.cs:39-53)

- `EmoteRegistry.AllEmotes` enumerates all registered emote names (case-insensitive
  store; keys are as registered). (src/Tapestry.Engine/EmoteRegistry.cs:37)

## Rejected and Reverted

- None on record.

## Change Log
