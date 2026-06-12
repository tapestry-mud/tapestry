---
capability: rest-and-recovery
last-updated: 2026-06-12
---

# Rest and Recovery

## Overview

The rest system tracks a per-entity rest state (standing, resting, or sleeping) and uses
that state as a multiplier on HP, resource, and movement regeneration. All configuration
is data-driven: packs set room healing rates in YAML, and pack scripts call into the `rest`
JS module. No rest or regen values are hardcoded in the engine.

The regen tick fires every 30 game-loop ticks (3 seconds at the default 100 ms/tick rate)
and is wired by `TickHandlerModule`. Rest multipliers, furniture bonuses, and room healing
rates are applied together each regen tick.

---

## Behavior

### Rest states

- **[RestStates]** An entity has one of three rest states stored in the transient property
  `rest_state`: `awake` (default when property is absent), `resting`, or `sleeping`.
  `src/Tapestry.Engine/Rest/RestProperties.cs`; `src/Tapestry.Engine/Rest/RestService.cs`

- **[RestService.Transition]** `SetRestState` publishes a cancellable
  `entity.rest_state.changed` event before applying the new state. Event data includes
  `entityId`, `oldState`, and `newState`. If the event is cancelled, no state change is
  made and `(false, "cancelled")` is returned. Attempting to transition to the current
  state returns `(false, "already_in_state")` without publishing an event. Transitioning
  to `awake` clears `rest_target`. Transitioning to `sleeping` records the current game
  loop tick count in `sleep_start_tick`.
  `src/Tapestry.Engine/Rest/RestService.cs`

- **[RestService.Furniture]** An optional furniture entity ID may be passed to
  `SetRestState`. When provided, its ID is stored in `rest_target`. During the regen tick
  the furniture entity's `rest_bonus` property (int) is read and added additively to the
  rest multiplier.
  `src/Tapestry.Engine/Rest/RestService.cs`; `src/Tapestry.Engine/GameLoop.cs` (line 467)

### RestConfig and multipliers

- **[RestConfig.Multipliers]** The base regen multiplier per state is:
  `awake` -> 1.0 (fixed), `resting` -> 2.0 (default), `sleeping` -> 3.0 (default).
  The resting and sleeping values are configurable via `RestConfig.Configure`.
  `src/Tapestry.Engine/Rest/RestConfig.cs`

- **[RestConfig.RoomBonus]** If an entity has a location, the room's `healing_rate`
  property (int, registered for `EntityTypes.Room`) is added to the final multiplier.
  This stacks additively with the state multiplier and any furniture bonus.
  `src/Tapestry.Engine/GameLoop.cs` (line 476); `src/Tapestry.Engine/Rest/RestProperties.cs`

- **[RestConfig.MinSleep]** `RestConfig` stores `MinSleepTicksForWellRested` (default 120,
  configurable). `SleepStartTick` is recorded on the entity when sleep begins. No call
  site in the current codebase reads either field to apply a "well rested" bonus; both
  are dead fields reserved for a future feature.
  `src/Tapestry.Engine/Rest/RestConfig.cs` (DEAD FIELD -- no consumer found)

### RestModule JS API

- **[RestModule.JS]** Pack scripts access rest via the `rest` JS namespace:
  - `rest.getRestState(entityId)` -- returns the rest state string; defaults to `"awake"`
    when no state is set.
  - `rest.setRestState(entityId, newState, furnitureId?)` -- transitions the entity and
    returns `{ success, reason }`.
  `src/Tapestry.Scripting/Modules/RestModule.cs`

### Regen tick

- **[Regen.Registration]** `TickHandlerModule.Configure` calls
  `GameLoop.RegisterRegenHandler(_world, _eventBus, regenIntervalTicks: 30, restConfig: _restConfig)`.
  The handler is registered under the name `"regen"` with a 30-tick interval (3 seconds
  at 100 ms/tick).
  `src/Tapestry.Server/Modules/TickHandlerModule.cs` (lines 90-92)

- **[Regen.Candidates]** Each regen tick collects all `player` and `npc` entities that do
  not have the `no_regen` tag. For each candidate the handler computes a `finalMultiplier`
  by summing the state multiplier, the furniture `rest_bonus`, and the room `healing_rate`.
  A `finalMultiplier` of exactly 0.0 causes the entity to be skipped entirely.
  `src/Tapestry.Engine/GameLoop.cs` (lines 450-482)

- **[Regen.Event]** For each vital that has regen headroom (current < max) the handler
  publishes an `entity.regen` event with data `{ "vital": "<hp|resource|movement>",
  "amount": <computed int> }`. The event is cancellable: a subscriber may set
  `evt.Cancelled = true` to suppress the regen application, or may mutate `amount` in the
  data dictionary to adjust the final value. The engine reads `amount` back from the
  dictionary after all subscribers run.
  `src/Tapestry.Engine/GameLoop.cs` (lines 493-549)

- **[Regen.Application]** If the event is not cancelled the engine adds the (possibly
  mutated) `amount` value directly to the entity stat. Separate `entity.regen` events are
  fired for each of the three vitals; each may be independently cancelled or mutated.
  `src/Tapestry.Engine/GameLoop.cs` (lines 502-549)

### Player commands

- **[RestCommands.PlayerFacing]** Three commands are registered by separate files under
  `packs/@tapestry/core/scripts/commands/`:
  - `rest` (`rest.js`) -- transitions the entity to `resting`. Blocked if already resting
    or sleeping, or if in combat. Calls `tapestry.rest.setRestState(entityId, 'resting', null)`.
  - `sleep` (`sleep.js`) -- transitions the entity to `sleeping`. Blocked if already
    sleeping or if in combat. Calls `tapestry.rest.setRestState(entityId, 'sleeping', null)`.
  - `wake` / `stand` (`wake.js`) -- transitions the entity to `awake` from any state.
    Calls `tapestry.rest.setRestState(entityId, 'awake')`.
  Both `rest` and `sleep` are available to roles `player` and `mob`; `wake` is also
  available to both.
  `packs/@tapestry/core/scripts/commands/rest.js`;
  `packs/@tapestry/core/scripts/commands/sleep.js`;
  `packs/@tapestry/core/scripts/commands/wake.js`

---

## Rejected and Reverted

No reversals on record as of 2026-06-12.

---

## Change Log

| Change Record | Summary |
|---------------|---------|
