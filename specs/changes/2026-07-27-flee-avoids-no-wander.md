---
release: unreleased
specs: [mob-ai.md, combat-resolution.md]
---

# Flee Avoids no_wander Destinations

## Why

The `wander` behavior already respected the pack-declared `no_wander` room tag ("Mobs will
not wander into this room"), but `CombatManager.AttemptFlee` did not: a fleeing low-HP trash
mob could pick a random exit straight into an adjacent boss room, turning what should be a
clean first contact into an unplanned 2v1. `no_wander` also lived only as a `tags.yml`
declaration in `@tapestry/core`, with no engine-side registration and no writer binding for
packs (like oracle) that want to tag a room at runtime.

## What

`no_wander` is now an engine tag (`EngineTags.NoWander`), registered the same way as
`persistent`/`no_kill`/`safe` -- any pack can apply it without a `tags.yml` declaration.
The pack-level declaration in `@tapestry/core/tags.yml` was removed as redundant.

`CombatManager.AttemptFlee` now filters its exit-selection to prefer exits whose target
room is not tagged `no_wander`. If every exit leads to a `no_wander` room (or there are no
exits at all), it falls back to the unfiltered exit list -- the intent is "prefer not to
enter," not "never flee if every path leads to one," so a cornered mob can still escape
somewhere rather than getting stuck.

A new write binding, `ApiWorld.AddRoomTag` / `tapestry.world.addRoomTag(roomId, tag)`, lets
pack scripts tag a room at runtime (previously only a reader, `getRoomTags`, existed).

## Consumer

`@tapestry/oracle` uses `tapestry.world.addRoomTag` to tag boss rooms `no_wander` at spawn
time, so trash mobs spawned nearby cannot flee into them.
