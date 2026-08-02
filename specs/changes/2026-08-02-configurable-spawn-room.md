---
release: unreleased
specs: [flows-and-wizards.md, world-entity-store.md]
---

# Configurable Spawn Room

## Why

`FlowEngine.DefaultSpawnRoomId` was a settable property that nothing in the tree ever set,
and `PlayerSpawner`'s login fallback was a private constant. Both read
`tapestry-core:recall`. There was no way for a world pack to say where its players wake up.

That room is the engine's own starting room, and its description is written to engine
builders rather than to players: it tells the reader the world beyond is "a blank slate
waiting for content" and to "use the `link` command to connect your rooms to a content
pack". Every brand-new character in every game read that copy first, because the only way
for a world to move them somewhere else was a pack script hook that necessarily runs after
the character is already placed and the first room has already rendered.

It also made the pack hook load-bearing for basic placement: when that hook failed, the
player was left standing in the engine's two-room fallback pocket with no exit into the
game and no way to reach it.

## What

`game.spawn_room` (`GameSection.SpawnRoom`) names the room a character with no saved
location wakes up in, and the room used as the fallback when a saved location no longer
exists. It defaults to `tapestry-core:recall`, so a game that does not set it is unchanged.

`ConfigurationModule` applies it to `FlowEngine.DefaultSpawnRoomId` at boot, and
`PlayerSpawner`'s fallback now reads the same config value rather than its own constant --
so the chargen path and the returning-player path agree on one answer.

A world that sets this never renders the engine's builder-facing room to a player, and its
opening room is where players land whether or not any pack script runs.
