---
release: pending
specs: [mob-lifecycle.md, area-authoring.md]
---

# Solo Oracle E1: Frozen Spawn Override + Replay

## Why

The solo-oracle slice 1 needs per-instance mob stats that survive area resets.
A LLM-generated mob gets a rolled name, HP, and damage expression; those facts
must be frozen so the same mob returns after a respawn rather than getting
re-rolled to a random variant. This is the engine seam that delivers that.

## What

- **`SpawnOverride` record** (`src/Tapestry.Engine/Mobs/SpawnOverride.cs`): a
  sealed blob carrying `FromType`, `Name`, `Desc`, `MaxHp`, `Damage`, `Items`,
  and `NoReroll`. Captured at oracle generation time; re-applied verbatim on
  every (re)spawn.

- **`SpawnRule.Override`**: `SpawnOverride?` field added to `SpawnRule`; copied
  from the `RegisterRoomSpawns` tuple into the registered rule.

- **`SpawnManager.SpawnMob(templateId, roomId, over?)`**: new optional third
  parameter. After `MobStatDerivation.Apply`, if `over != null`, calls the new
  `ApplyOverride` helper which writes name, description, `BaseMaxHp`/`Hp`,
  `damage_dice`, `oracle_from_type`, and contents items from the blob.

- **`SpawnManager.RegisterRoomSpawns`**: tuple gains `Override: SpawnOverride?`.
  All existing callers (PackLoader) pass `null`. `RunAreaReset` skips the
  rare-swap when a frozen override is present and passes the override through to
  `SpawnMob`, so the identical instance returns after death.

- **Sidecar round-trip** (`src/Tapestry.Engine/Authoring/RoomSpawnData.cs`,
  `RoomData.Spawns`): `RoomData` gains a `List<RoomSpawnData>` `Spawns` field.
  Each entry carries `Base` (template ID) and an optional `RoomSpawnOverrideData`
  sub-object. `RoomProjector` populates this from `SpawnManager.GetRoomSpawns`
  when a `SpawnManager` is injected (optional parameter, default null).
  The `OmitEmptyCollections` serializer config ensures the field is absent from
  pack-room YAML when empty.

- **JS binding**: `mobs.spawnMob(options)` now accepts a single options object
  `{ template, roomId, override?: { ... } }` per the Jint single-JsValue
  convention. Previous 2-arg pack JS that calls `spawnMob(template, roomId)`
  will see a null return (ObjectInstance check fails); pack JS should migrate
  to the options form.
