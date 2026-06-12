---
capability: world-and-entities
last-updated: 2026-06-12
---

# World and Entities

## Overview

World is the central in-memory store for all live game objects. It owns three
parallel indexes: a room dictionary, an entity dictionary, and a copy-on-write
tag index. Entity is the universal runtime object -- players, NPCs, items, and
corpses are all Entity instances distinguished by a Type string. Areas are
metadata-only descriptors registered separately in AreaRegistry; rooms are the
physical nodes that belong to areas. DoorService manages open/close/lock state
for exits that carry a DoorState. TemporaryExitService adds and expires
keyword-keyed exits (portals) without modifying permanent room data. ActorContext
is a lightweight command-dispatch carrier. WorldCensus is a diagnostic snapshot
sampled on demand.

## Behavior

### World -- entity store

- World holds entities in a `Dictionary<Guid, Entity>` keyed by entity.Id.
  (src/Tapestry.Engine/World.cs:10)
- `TrackEntity` adds an entity to the dictionary and registers World as a tag
  observer, seeding the write index with any tags already on the entity.
  (src/Tapestry.Engine/World.cs:184-192)
- `UntrackEntity` removes the entity and deregisters the observer, stripping
  all its tags from the write index.
  (src/Tapestry.Engine/World.cs:194-202)
- `UntrackEntityDeep` recursively untracks everything in Equipment and Contents
  before the entity itself, preventing orphaned tag-index memberships on despawn.
  (src/Tapestry.Engine/World.cs:212-223)
- `GetEntity(Guid)` checks the dictionary first, then falls back to scanning
  room entity lists (back-fills the dictionary on hit), then queries
  PlayerCreator for entities mid-creation.
  (src/Tapestry.Engine/World.cs:225-244)
- `GetEntitiesByType` filters the dictionary by case-insensitive Type match;
  no index, linear scan.
  (src/Tapestry.Engine/World.cs:261-264)
- `GetEntitiesInRoom` delegates to Room.Entities for the named room.
  (src/Tapestry.Engine/World.cs:318-322)
- `GetEntitiesByTemplateId` scans the dictionary for a CommonProperties.TemplateId
  match; no dedicated index.
  (src/Tapestry.Engine/World.cs:311-316)
- `GetAllTrackedEntities` returns the raw dictionary values snapshot.
  (src/Tapestry.Engine/World.cs:247-249)

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

### Tag index -- copy-on-write double buffer

- World maintains two tag-set dictionaries: `_readIndex` (stable, read by
  game logic) and `_writeIndex` (mutated by tag add/remove during the tick).
  (src/Tapestry.Engine/World.cs:13-14)
- Tag mutations in a tick never appear in `GetEntitiesByTag` until
  `SwapTagBuffers` is called. Tests confirm the read index is unchanged
  mid-tick. (src/Tapestry.Engine/World.cs:324-331;
  tests/Tapestry.Engine.Tests/WorldTagIndexTests.cs:9-53)
- `SwapTagBuffers` promotes _writeIndex to _readIndex and copies it as the
  new _writeIndex; `_dirtyTags` is cleared. Records LastSwapDirtyCount and
  LastSwapTagCount for diagnostics.
  (src/Tapestry.Engine/World.cs:324-331)
- On first mutation of a tag within a tick the write-side set is cloned from
  the read snapshot (copy-on-write); subsequent mutations in the same tick
  operate on the already-cloned set. Undirtied tags share the same set
  reference across swaps.
  (src/Tapestry.Engine/World.cs:343-357;
  tests/Tapestry.Engine.Tests/WorldTagIndexTests.cs:151-172)
- When all members are removed the write-side key is pruned (empty set deleted)
  but the tag remains in _dirtyTags so SwapTagBuffers will not re-clone from
  the stale read snapshot. A subsequent removal on a dirty-pruned tag is a no-op,
  not a KeyNotFoundException.
  (src/Tapestry.Engine/World.cs:360-382;
  tests/Tapestry.Engine.Tests/WorldTagIndexTests.cs:116-149)
- `GetEntitiesByTag` returns IReadOnlySet<Entity>; returns an empty immutable
  set if the tag is absent.
  (src/Tapestry.Engine/World.cs:252-259)

### Entity -- identity and type

- Each Entity has an immutable Guid Id assigned at construction (or supplied
  via optional parameter), a mutable Type string, and a mutable Name string.
  (src/Tapestry.Engine/Entity.cs:10-12,35-40)
- LocationRoomId is null when the entity is inside a container or has no
  placement; Container holds the owning entity when inside one.
  (src/Tapestry.Engine/Entity.cs:13-14)

### Entity -- property bag

- Properties are stored in `Dictionary<string, object?>` keyed by string.
  Setting a key to null removes the entry.
  (src/Tapestry.Engine/Entity.cs:16,43-51)
- `GetProperty<T>` attempts direct cast, then coerces List<object> to
  List<string> for list properties, then performs numeric type coercion (Jint
  stores numbers as double; C# callers often want int). bool and string are
  never coerced.
  (src/Tapestry.Engine/Entity.cs:54-74,98-128)
- `SetMapValue` / `GetMapValue` / `GetMap` / `RemoveMapKey` manage nested
  `Dictionary<string, T>` values under a single property key.
  (src/Tapestry.Engine/Entity.cs:135-179)
- `PropertyCount` returns the number of entries; used by WorldCensus.
  (src/Tapestry.Engine/Entity.cs:187)
- CommonProperties defines engine-reserved string constants for: template_id,
  source_pack, class, race, alignment, description, regen_hp/resource/movement,
  corpse_decay, corpse_created_tick, screen_width, and transient group/follow
  keys. Registered via PropertyRegistry at boot.
  (src/Tapestry.Engine/CommonProperties.cs:7-51)

### Entity -- tags, keywords, roles

- Tags are case-insensitive HashSet<string>. `AddTag` / `RemoveTag` notify all
  registered ITagObserver instances; no-ops if the set is unchanged.
  (src/Tapestry.Engine/Entity.cs:201-221)
- Keywords are case-insensitive; no observer notification.
  (src/Tapestry.Engine/Entity.cs:229-235)
- Roles are case-insensitive; support Add / Has / Remove.
  (src/Tapestry.Engine/Entity.cs:237-251)
- `RegisterTagObserver` / `UnregisterTagObserver` manage the observer list.
  (src/Tapestry.Engine/Entity.cs:253-261)

### Entity -- inventory and equipment

- `AddToContents` removes the item from any prior container, clears its
  LocationRoomId, and sets Container. `RemoveFromContents` clears Container.
  (src/Tapestry.Engine/Entity.cs:263-277)
- Equipment is a `Dictionary<string, Entity>` keyed by slot name
  (case-insensitive).
  (src/Tapestry.Engine/Entity.cs:21,279-292)

### ITagObserver

- Interface with two methods: `OnTagAdded(entity, tag)` and
  `OnTagRemoved(entity, tag)`. World implements this interface to drive the
  CoW tag index.
  (src/Tapestry.Engine/ITagObserver.cs:1-7;
  src/Tapestry.Engine/World.cs:7,333-341)

### TagRegistry

- Engine tags are registered under their bare snake_case name; pack tags under
  `packName:name`. Pack tags cannot shadow engine tag names.
  (src/Tapestry.Engine/Tags/TagRegistry.cs:11-43)
- Tag names must match `^[a-z][a-z0-9_]*$`; hyphens throw ArgumentException.
  (src/Tapestry.Engine/Tags/TagRegistry.cs:84-98)
- `TryResolve(tag, currentPack)` first tries a direct key lookup, then a
  pack-prefixed lookup, then each declared dependency pack in order.
  (src/Tapestry.Engine/Tags/TagRegistry.cs:51-78)
- `TagRegistryEntry` carries Name, Scope, Description, AppliesTo, Kind, and
  FullName (engine tags: bare name; pack tags: `scope:name`).
  (src/Tapestry.Engine/Tags/TagRegistryEntry.cs:1-15)

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
  `areaPrefix:`.
  (src/Tapestry.Engine/DoorService.cs:99-109)
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
  source-side record triggers expiry; the partner is cleaned up atomically in
  the same lock.
  (src/Tapestry.Engine/TemporaryExitService.cs:149-182)
- `RemoveExit` removes both sides if paired; publishes portal.closed per
  removed exit. Creation publishes portal.opened.
  (src/Tapestry.Engine/TemporaryExitService.cs:123-147)

### ActorContext

- ActorContext is an immutable record carrying EntityId, Name, RoomId, Source
  ("player" or "mob"), RawInput, Command, and RawArgs. It is not persisted.
  (src/Tapestry.Engine/ActorContext.cs:1-12)

### WorldCensus

- WorldCensus is a diagnostic snapshot: EntitiesByType (count per type),
  TagCount (distinct tag keys), TagMemberships (sum of set sizes),
  PropertiesTotal (sum of property-bag entry counts), and MaxEntityProperties
  (largest single bag).
  (src/Tapestry.Engine/WorldCensus.cs:1-16)
- `World.SampleCensus` computes the snapshot on demand, iterating tracked
  entities and the read index. Returns null on any exception caused by
  concurrent structural mutation so telemetry never throws into the engine.
  (src/Tapestry.Engine/World.cs:273-309)

## Rejected and Reverted

_No tombstones recorded._

## Change Log

| Change Record | Summary |
|---------------|---------|

---

Sources consulted:
- src/Tapestry.Engine/World.cs (384 lines)
- src/Tapestry.Engine/Entity.cs (293 lines)
- src/Tapestry.Engine/AreaDefinition.cs (22 lines)
- src/Tapestry.Engine/AreaRegistry.cs (12 lines)
- src/Tapestry.Engine/Room.cs (165 lines)
- src/Tapestry.Engine/Exit.cs (14 lines)
- src/Tapestry.Engine/DoorService.cs (195 lines)
- src/Tapestry.Engine/DoorState.cs (18 lines)
- src/Tapestry.Engine/ITagObserver.cs (7 lines)
- src/Tapestry.Engine/ActorContext.cs (12 lines)
- src/Tapestry.Engine/WorldCensus.cs (16 lines)
- src/Tapestry.Engine/CommonProperties.cs (52 lines)
- src/Tapestry.Engine/Tags/TagRegistry.cs (99 lines)
- src/Tapestry.Engine/Tags/TagRegistryEntry.cs (15 lines)
- src/Tapestry.Engine/TemporaryExitService.cs (212 lines)
- src/Tapestry.Scripting/Modules/WorldModule.cs (506 lines)
- src/Tapestry.Scripting/Modules/PortalsModule.cs (74 lines)
- tests/Tapestry.Engine.Tests/WorldTests.cs (109 lines)
- tests/Tapestry.Engine.Tests/WorldDoorTests.cs (84 lines)
- tests/Tapestry.Engine.Tests/WorldTagIndexTests.cs (173 lines)
- git log --oneline -15 -- src/Tapestry.Engine/World.cs src/Tapestry.Engine/Entity.cs

UNVERIFIED count: 0 -- all claims have inline source anchors.

SPLIT SUGGESTION: The Behavior section covers 9 sub-systems and is ~220 lines.
Consider splitting into world-entity-store.md (World index, Entity, tags) and
world-geography.md (rooms, areas, exits, doors, portals) when either grows
further.

Out-of-scope notes:
- Mob spawning (MobSpawner, spawn tables) -- see mob-ai.md
- Player persistence (save/load) -- see persistence.md
- Area authoring commands (dig, rekey, room-edit) -- see area-authoring.md
- Output rendering (ANSI, paging, word-wrap) -- see output-pipeline.md
- PropertyRegistry (property metadata store) -- referenced but not detailed here;
  candidate for a registries-and-seal.md addendum
- WorldModule JS API surface is broad; only entity/room/tag access methods noted here.
  Map projection (renderAreaMap, projectArea) is cosmetic output, deferred to
  output-pipeline.md scope.
- AreaMapProjector / AsciiMapRenderer are out of scope for this spec.
