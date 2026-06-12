---
capability: world-entity-store
last-updated: 2026-06-12
---

# World Entity Store

## Overview

World is the central in-memory store for all live game objects. It owns three
parallel indexes: a room dictionary, an entity dictionary, and a copy-on-write
tag index. Entity is the universal runtime object -- players, NPCs, items, and
corpses are all Entity instances distinguished by a Type string. WorldCensus is
a diagnostic snapshot sampled on demand.

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
- `GetAllTrackedEntities` returns a live view of the internal dictionary values,
  not a snapshot; callers that need a stable copy must materialise one.
  (src/Tapestry.Engine/World.cs:247-249)

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
- CommonProperties defines engine-reserved string constants for:
  - `template_id` -- template used to spawn this entity
  - `source_pack` -- pack that loaded the entity (transient)
  - `class` -- character class
  - `race` -- character race
  - `alignment` -- alignment value (-1000 to 1000)
  - `description` -- entity description text
  - `regen_hp` / `regen_resource` / `regen_movement` -- per-tick regeneration
  - `corpse_decay` -- ticks until corpse decays
  - `corpse_created_tick` -- world tick when corpse was created
  - `screen_width` -- preferred output width in columns
  - `last_tell_from` -- last entity who sent a tell to this player (transient)
  - `last_tell_to` -- last entity this player sent a tell to (transient)
  - transient group/follow keys: `alignment_history`, `no_follow`, `following`,
    `group_id`, `group_leader`, `group_join_time`, `group_invite_from`,
    `group_invite_expires`
  Registered via PropertyRegistry at boot.
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

- None on record.

## Change Log
