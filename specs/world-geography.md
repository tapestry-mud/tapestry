---
capability: world-geography
last-updated: 2026-06-22
---

# World Geography

## Overview

World owns a room dictionary as one of its three parallel indexes. Areas are
metadata-only descriptors registered separately in AreaRegistry; rooms are the
physical nodes that belong to areas. DoorService manages open/close/lock state
for exits that carry a DoorState. TemporaryExitService adds and expires
keyword-keyed exits (portals) without modifying permanent room data.

ActorContext (entity + room context for command dispatch) is documented in
command-dispatch.md.

## Behavior

### World -- room store

- World holds rooms in a `Dictionary<string, Room>` keyed by room.Id.
  (src/Tapestry.Engine/World.cs:9)
- `AddRoom` / `GetRoom` / `RemoveRoom` are direct dictionary operations.
  (src/Tapestry.Engine/World.cs:25-38)
- `AllRooms` exposes the dictionary values.
  (src/Tapestry.Engine/World.cs:104)
- `RekeyRoom` atomically re-indexes a room, retargets same-area exits, and
  updates LocationRoomId on every entity standing in the room. Pack rooms and
  cross-area referencing rooms are reported as edges but not mutated.
  (src/Tapestry.Engine/World.cs:46-102)

### World -- movement

- `MoveEntity(entity, direction)` (no door service) resolves the exit and moves
  unconditionally; returns false if the entity has no room, the exit is missing,
  or the target room is unknown.
  (src/Tapestry.Engine/World.cs:106-134)
- `MoveEntity(entity, direction, doorService, eventBus)` additionally blocks on
  a closed door and publishes a `door.blocked` event before returning false.
  (src/Tapestry.Engine/World.cs:136-182)

### Area and room model

- AreaDefinition holds metadata: Id, Name, LevelRange, ResetInterval,
  OccupiedModifier, WeatherZone, Flags, and weather/time message maps.
  SourcePack is transient, never serialized.
  (src/Tapestry.Engine/AreaDefinition.cs:3-22)
- AreaRegistry is a case-insensitive dictionary of AreaDefinition. It does not
  own rooms; rooms carry an Area string referencing the area Id.
  (src/Tapestry.Engine/AreaRegistry.cs:1-12;
  src/Tapestry.Engine/Room.cs:25)
- Room stores exits in two dictionaries: `Dictionary<Direction, Exit>` for
  cardinal/intercardinal exits and `Dictionary<string, Exit>` (case-insensitive)
  for keyword exits.
  (src/Tapestry.Engine/Room.cs:16-18)
- Room.AddEntity sets entity.LocationRoomId to the room's Id and removes the
  entity from any container. Room.RemoveEntity clears LocationRoomId.
  (src/Tapestry.Engine/Room.cs:114-130)
- Room has its own tag and property bags (not tracked in the World tag index;
  no observer plumbing).
  (src/Tapestry.Engine/Room.cs:20-21,132-162)
- Room.Id has an `internal set` -- only World.RekeyRoom may mutate it.
  (src/Tapestry.Engine/Room.cs:11)

### Exits

- Exit carries TargetRoomId (mutable for retargeting), an optional DoorState,
  optional Conditions map, and optional DisplayName.
  (src/Tapestry.Engine/Exit.cs:1-14)
- `Room.HasExitTo` checks both directional and keyword exit targets.
  (src/Tapestry.Engine/Room.cs:83-87)
- `Room.RetargetExits` repoints all matching exits in both dictionaries; used
  by RekeyRoom.
  (src/Tapestry.Engine/Room.cs:92-112)

### Runtime exit collapse (ConsequenceOverlay.CollapseExit)

- `ConsequenceOverlay.CollapseExit(roomId, direction, kind, lifespan)` removes a
  directional exit from the live World graph in memory only. It calls
  `Room.RemoveExit` directly and never writes a sidecar. On reboot the overlay is
  empty and the sidecar reload restores the original exit, so the collapse cleanly
  evaporates. This upholds the "connection exits never persist" invariant by
  construction. (src/Tapestry.Engine/Consequence/ConsequenceOverlay.cs)
- Distinct from `tapestry.authoring.setRoomExit`/`removeRoomExit`, which both
  call `WriteSideCar` and persist the change across reboots.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs)
- Returns false when the room id is unknown or the direction string cannot be
  parsed. Otherwise it removes the exit and records a consequence entry under the
  caller-supplied opaque `kind` and `lifespan` (an empty `kind` removes the exit
  without recording). The engine never names the content - the pack supplies the
  kind (e.g. `collapsed`/`succession-seed`), keeping the engine content-agnostic.
- Exposed as the JS binding
  `tapestry.consequence.collapseExit(roomId, direction, kind, lifespan)`.
  (src/Tapestry.Scripting/Modules/ConsequenceModule.cs)

### Door system

- DoorState carries Name, IsClosed, IsLocked, KeyId, IsPickable, PickDifficulty,
  DefaultClosed, and DefaultLocked. Keywords are derived from the Name by
  splitting on spaces.
  (src/Tapestry.Engine/DoorState.cs:1-18)
- DoorService.Open / Close / Unlock / Lock mutate the exit's DoorState and
  synchronise the reverse exit (opposite direction in the target room) via
  SyncReverse.
  (src/Tapestry.Engine/DoorService.cs:18-59)
- Locking requires the door to already be closed; unlocking does not require a
  key in hand -- HasKey is a query helper only.
  (src/Tapestry.Engine/DoorService.cs:52-54,76-81)
- `ResetDoor` restores IsClosed / IsLocked to DefaultClosed / DefaultLocked and
  syncs the reverse.
  (src/Tapestry.Engine/DoorService.cs:85-97)
- `ResetArea(areaPrefix)` resets every door in every room whose Id starts with
  `areaPrefix:` (prefix match). It also matches a room whose Id equals
  areaPrefix exactly (case-insensitive), covering single-room area edge cases.
  (src/Tapestry.Engine/DoorService.cs:99-109,190-194)
- `ResolveTarget` resolves a door target from raw input: exact direction first,
  then keyword with optional ordinal prefix (`2.gate`).
  (src/Tapestry.Engine/DoorService.cs:113-153)
- State changes publish typed GameEvents (door.opened, door.closed,
  door.locked, door.unlocked); lock/unlock events include keyId in the payload.
  (src/Tapestry.Engine/DoorService.cs:169-188)

### Portal system (TemporaryExitService)

- Portals are keyword-keyed exits added to a room at runtime and tracked in an
  internal dictionary with an expiry tick count.
  (src/Tapestry.Engine/TemporaryExitService.cs:10,21-57)
- `CreateExit` adds a single keyword exit; `CreatePairedExit` adds symmetric
  exits at both ends in one atomic call. Both reject if the keyword slot is
  already occupied. Both return a string exit Id (empty string on failure).
  (src/Tapestry.Engine/TemporaryExitService.cs:21-121)
- Portals expire on `area.tick` events. When a paired portal expires only the
  source-side record triggers expiry and fires a portal.closed event; the
  partner exit is removed from its room atomically in the same lock without
  firing an additional portal.closed event.
  (src/Tapestry.Engine/TemporaryExitService.cs:149-182)
- `RemoveExit` removes both sides if paired; publishes portal.closed per
  removed exit. Creation publishes portal.opened.
  (src/Tapestry.Engine/TemporaryExitService.cs:123-147)

### Stub exits and lazy-mint movement

- `Exit.IsStub` (bool) marks an exit as a placeholder pointing at no real room
  (TargetRoomId is empty string). Stub exits are serializable and survive
  side-car write/reload. Movement through a stub exit requires a registered
  `StubExitResolver` to mint the neighbor room; without one the move fails
  gracefully (returns false, entity stays in place).
  (src/Tapestry.Engine/Exit.cs; src/Tapestry.Engine/StubExitResolver.cs)

- `World.MoveEntity(entity, direction, resolver?)` (simple overload) accepts an
  optional `StubExitResolver`. When the exit is a stub, it calls
  `resolver.TryResolve(roomId, directionString)`. The resolver is expected to
  mint the neighbor room, wire the exit to a real room id, and return true.
  `MoveEntity` re-fetches the exit after TryResolve; if it is still a stub or
  null the move returns false.
  (src/Tapestry.Engine/World.cs:MoveEntity)

- `World.MoveEntity(entity, direction, doorService, eventBus, resolver?)` (door-
  aware overload) applies the same stub check before the door check.
  (src/Tapestry.Engine/World.cs:MoveEntity)

- `StubExitResolver` is a singleton delegate registry. `Register` replaces the
  single resolver; `TryResolve` calls it and returns false on exception or if no
  resolver is registered. The resolver receives (roomId, directionString).
  (src/Tapestry.Engine/StubExitResolver.cs)

- `ApiWorld.MoveEntity` passes the injected `StubExitResolver` to `World.MoveEntity`,
  so oracle-controlled movement is automatic for all pack movement calls.
  (src/Tapestry.Scripting/Services/ApiWorld.cs)

## Rejected and Reverted

- None on record.

## Change Log

- 2026-06-22 [solo-oracle-engine-seams](changes/2026-06-22-solo-oracle-engine-seams.md) - E3 stub exits + lazy-mint movement: `Exit.IsStub`, `StubExitResolver` delegate registry, `World.MoveEntity` resolver overloads, `ExitData` POCO + YAML converter, `setStubExit`/`registerStubResolver` JS bindings
