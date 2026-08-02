---
release: unreleased
specs: [mob-ai.md]
---

# Mob Movement Is Switchable For Deterministic Scenarios

## Why

The telnet scenario suite is the engine's end-to-end gate -- coverage for what unit and
functional tests cannot reach -- and `publish` sits behind it, so a release image cannot
exist without it green. It was not deterministic. A different scenario failed almost every
run: `kill tandoor` after "tandoor beast leaves north", an hp assertion on a mob that had
already moved, a room report missing the occupant it was written to describe. Four different
scenarios failed across five runs, and three runs of one unchanged commit failed on three
different sets -- the signature of a shared timing fault rather than any broken scenario.

The cause is that mobs relocate underneath a running scenario. `wander` picks a random exit
on a random roll (two unseeded `Math.random()` calls); `patrol` follows a fixed route but on
a timer whose phase relative to a scenario is arbitrary. Either way a mob can leave between
the command that finds it and the command that acts on it, and an arrive/leave line can land
in the middle of output an assertion is reading. Five of the twenty-one scenario files act on
wander-capable mobs, and those five were exactly the ones failing.

The `no_wander` tag could not fix this: it is checked against a mob's DESTINATION, so it
stops mobs entering a tagged room and never stops the occupant of that room leaving. Pinning
per-mob is already covered by `behavior: stationary`, but that is an authoring-time choice on
templates a scenario does not own -- oracle mints its trash from `hostile/skittish/wary-melee`,
all of which wander by design, and the scenario simply walks up to whatever the seed placed.

## What

`mob_ai.movement_enabled` (`MobAiSection.MovementEnabled`, default `true`) says whether mobs
may relocate. `server.test.yaml` -- the config the managed scenario runner copies -- sets it
`false`, so the whole suite runs with mobs held still.

The engine does not skip behaviors itself; it exposes the answer as
`tapestry.mobs.movementEnabled()` and the behaviors consult it. Movement semantics stay owned
by the pack that defines them, and a pack can gate a new movement behavior the same way.

Deliberately NOT done: an NPC-scoped `no_wander`. A mob that should hold ground already has
`behavior: stationary`; a second mechanism for the same thing would only be a second place to
look. `no_wander` stays room-scoped and destination-gating.

This does not weaken `boss-room-no-wander`, the one scenario about the tag -- it asserts the
room CARRIES `no_wander`, and never exercises live wandering.
