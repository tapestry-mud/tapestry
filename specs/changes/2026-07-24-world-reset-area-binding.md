---
release: 0.1.53
specs: [mob-lifecycle.md]
---

# World Reset-Area Binding

## Why

`SpawnManager.RunAreaReset` already existed, but it only ran off the `area.tick` cadence
(every `reset_interval` ticks, slowed by `occupied_modifier` in populated areas). A consumer
that needs an area's authored spawn-rule content repopulated *now* -- not on the next tick
window -- had no way to ask for it. The game-hub-threads grind-death path is that consumer:
when a player dies in a grind-tier run and respawns at the entry, the run's mobs must be back
immediately, not after a reset interval.

## What

`tapestry.world.resetArea(areaId)` (`WorldModule`) binds `RunAreaReset(areaId)` as an
on-demand scripting call. It runs the identical reset path as the `area.tick` subscription, so
there is no second code path to keep in sync. A null or whitespace id is a no-op; an id with
no registered spawn rules is a no-op, because the reset targets `_areaConfigs`, which is
populated only by `RegisterAreaSpawns`/`RegisterRoomSpawns`. That last point matters for the
consumer: content that spawns lazily (never through the engine's authored spawn-rule system)
is not touched by `resetArea` and must repopulate itself by other means.

## Consumer

`@tapestry/core`'s tier-scaled death handler (game-hub-threads plan) calls `resetArea` on a
grind-tier respawn. Because the oracle's own run mobs spawn lazily rather than via spawn rules,
that path pairs `resetArea` with a pack-side visited-state clear; see the packs' change record
for the full mechanism.
