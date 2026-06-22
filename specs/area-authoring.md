---
capability: area-authoring
last-updated: 2026-06-15
---

# Area Authoring

## Overview

Area authoring covers how packs define areas and rooms in YAML, how the engine
loads and registers them at boot, and how the runtime builder tools (dig, create,
edit) create new authored content that persists across reboots. Two separate storage
layers are involved: pack source files (checked into packs/) and runtime side-car
files (written under the game data root at runtime).

## Behavior

### Area YAML format

- An area file is a YAML document with a single top-level key `area:` containing
  fields: `id` (required), `name` (required), `short`, `description`, `theme`, `lore`,
  `level_range`, `reset_interval`, `occupied_modifier`, `weather_zone`, `flags`,
  `weather_messages`, `time_messages`.
  (src/Tapestry.Scripting/YamlContentLoader.cs:213-237, AreaDefinitionModel class;
  `short`/`description`/`theme`/`lore` mapped at YamlContentLoader.cs:719-722)

- `level_range` defaults to `[1, 99]`; `reset_interval` defaults to `3000` seconds;
  `occupied_modifier` defaults to `3.0`. (src/Tapestry.Engine/AreaDefinition.cs:11-13)

- `flags` is a list of strings; the engine recognizes the `wip` flag to mark an area
  as work-in-progress. WIP areas are excluded from the `areas` command listing for
  non-builders: `GetAreas(includeWip)` skips any area whose flags contain `wip` when
  `includeWip` is false. The area itself remains accessible; only the listing is gated.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:578-587;
  pack side: packs/@tapestry/core/scripts/commands/areas.js:15-16)

- Area files in packs are loaded by `PackLoader.LoadContent` before rooms. After rooms
  are loaded, the loader validates that every room whose YAML declares an `area:` field
  references a registered area, and throws if it does not. Rooms that omit the `area:`
  field entirely are loaded without that validation.
  (src/Tapestry.Scripting/PackLoader.cs:204-211)

### Room YAML format

- A room file is a YAML document with fields: `id` (required, format `namespace:key`),
  `name` (required), `description`, `area` (area id, no namespace), `exits`, `tags`,
  `biome`, `properties`, `spawns`, `fixtures`, `reset_interval`, `keyword_exits`,
  `alignment_range`, `alignment_block_message`, `weather_exposed`, `time_exposed`,
  `weather_messages`, `time_messages`, `entry_point_description`, `entry_point_direction`.
  (src/Tapestry.Scripting/YamlContentLoader.cs:686-708;
  `entry_point_description`/`entry_point_direction` at YamlContentLoader.cs:706-707)

- Exit values are either a string shorthand (`north: "pack:room-id"`) or an object
  with fields `target`, `name`, `door`. The `door` sub-object supports `name`, `closed`,
  `locked`, `key`, `pickable`, `pick_difficulty`.
  (src/Tapestry.Scripting/YamlContentLoader.cs:439-487)

- `biome:` is a single tag name with kind "biome" in the tag registry; placing it under
  `tags:` instead produces a load-time warning. (src/Tapestry.Scripting/YamlContentLoader.cs:92-103)

- `terrain` is read from the room's `properties:` map (key `"terrain"`); setting it to
  `indoors` or `underground` makes `weather_exposed` and `time_exposed` default to
  false; all other terrain values leave them exposed unless overridden.
  (src/Tapestry.Scripting/YamlContentLoader.cs:602-605)

### Pack file layout

- The engine discovers area definition files and room files via glob patterns declared
  in the pack's `pack.yaml` under the `content:` key. Area definitions use the
  `area_definitions:` field (glob relative to the pack directory, e.g.
  `areas/**/area.yaml`) and rooms use the `rooms:` field (e.g. `areas/**/rooms/*.yaml`).
  A pack that omits either field simply loads no content of that type; neither field is
  required. (packs/@tapestry/core/pack.yaml:15-16;
  packs/@tapestry/example-pack/pack.yaml:18-19)

- The conventional layout places each area's definition at
  `areas/<area-slug>/area.yaml` and its rooms at
  `areas/<area-slug>/rooms/<room-key>.yaml`, matching the glob patterns above.

### Boot loading order

- At boot, `PackLoader` loads pack area definitions and rooms; after all packs load,
  `AuthoredAreaLoader` upserts area side-cars (`<root>/<area>/area.yaml`), overlaying
  pack-defined fields while preserving the packed area's `SourcePack` value.
  (src/Tapestry.Scripting/Authoring/AuthoredAreaLoader.cs:14-67)

- `AuthoredRoomLoader` then applies room side-cars from `<root>/<area>/rooms/*.yaml`,
  skipping files named `area.yaml` to avoid misinterpreting area side-cars as rooms.
  (src/Tapestry.Scripting/Authoring/AuthoredRoomLoader.cs:44-48)

- Authored rooms that duplicate an already-loaded pack room are skipped with a warning,
  not rejected fatally. (src/Tapestry.Scripting/Authoring/AuthoredRoomLoader.cs:62-68)

- `ConnectionLoader` runs after rooms and applies cross-area exits from
  `<root>/connections/*.yaml` records into the live world.
  (src/Tapestry.Scripting/Connections/ConnectionLoader.cs:76-126)

### Runtime authoring (WorldAuthoringModule / tapestry.authoring.*)

- `createRoom(areaId, roomId, name, description)` requires the room id to contain `:`
  and the namespace before `:` to be in the set of loaded pack namespaces; duplicate ids
  are rejected. On success it writes a side-car immediately.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:146-168)

- `setRoomExit(roomId, dir, targetId)` and `removeRoomExit(roomId, dir)` both mutate
  the live world and rewrite the side-car in one step.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:318-352)

- `setRoomName(roomId, name)` renames the room and, for non-pack authored rooms, re-keys
  the id to a slug derived from the name. The old side-car file is deleted and a new one
  written under the slugged key; same-area neighbor side-cars are rewritten to point at
  the new id; cross-area connection records are updated via `ConnectionLoader.RetargetRoom`.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:170-267)

- Pack rooms (rooms with a `source_pack` property) can have their name updated but are
  never re-keyed; the id is owned by the pack.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:181-186)

- `deleteRoom(roomId)` removes the room from the live world and deletes its side-car.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:394-410)

- `createArea(areaId, name)` registers a new area in `AreaRegistry` and writes an
  `area.yaml` side-car; duplicate ids are rejected.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:626-640)

- Area mutators (`setAreaName`, `setAreaShort`, `setAreaDescription`, `setAreaTheme`,
  `setAreaLore`, `setAreaAttribute`) all update the live registry and rewrite the area
  side-car atomically. (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:518-576)

- `setAreaAttribute` accepts `level_range` (two-integer range), `reset_interval`
  (integer seconds), and `wip` (bool). Other attribute names are rejected.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:524-576)

### dig command (builder pack)

- `dig <dir>` carves a new room in the given direction from the builder's current room,
  mints a collision-free id (`namespace:area-N`), writes two-way exits, and teleports
  the builder into the new room. (packs/@tapestry/builder/scripts/commands/dig.js:129-150)

- `dig <dir> <target>` connects two existing authored rooms within the same area with a
  two-way exit; the reverse slot is wired unless already occupied, in which case a
  one-way exit is created with a warning. (packs/@tapestry/builder/scripts/commands/dig.js)

- `dig <dir>` from a pack-owned room (detected via `source_pack`) routes through a
  carve-into-pack branch instead of refusing: a shadow guard confirms the chosen
  direction is free on the pack room, the authored room is minted, and the boundary link
  is wired as a connection record via `tapestry.connections.create` rather than a side-car
  exit - so it never mutates pack data and survives restarts and pack updates. The builder
  gets an ASCII boundary message noting the way back lives outside the pack. Digging onward
  from the new authored room is the unchanged authored-to-authored path (inline side-car
  exits). (packs/@tapestry/builder/scripts/commands/dig.js)

- Shadow guard: before creating anything, the carve-into-pack branch checks
  `getExitTarget(fromId, dir)`. If that direction is already a real pack exit, dig refuses
  with "already taken" and changes nothing - a connection exit must never shadow the pack's
  own topology. (packs/@tapestry/builder/scripts/commands/dig.js)

- `dig <dir> <target>` (connect) still refuses when the from-room is pack-owned, and still
  refuses to connect to a pack room as a target. Outward growth only; splicing a new room
  into a different pack room is out of scope.
  (packs/@tapestry/builder/scripts/commands/dig.js)

### create command (builder pack)

- `create area <namespace:area-id>` creates a new area and an anchor room, then
  teleports the builder into the anchor. Area id must not already exist.
  (packs/@tapestry/builder/scripts/commands/create.js:32-68)

- `create room <key>` creates a room in the current area with no auto-exit and no move.
  (packs/@tapestry/builder/scripts/commands/create.js:72-96)

### Oracle headless recommend seam

- The recommend engine is callable from pack JS without an interactive builder session via
  `authoring.recommend(options, callback)` (see scripting-runtime.md for the full contract).
- The engine projects room context (neighbors, area, biome) using `RoomProjector` +
  `RoomPromptBuilder`'s neighbor-stitching path. The result is wrapped in a `PackRoomContext`
  carrying the projected `RoomData`, the pack-supplied template, system voice, and `vars` map.
  `LlmRecommendProvider` detects `PackRoomContext` as its first branch (before the existing
  hard `RoomData` cast) and routes to the pack-driven `RoomPromptBuilder.Build` overload.
- All LLM output passes through `OutputSanitizer.Clean` + `NormalizeSuggestion`, extended
  with an `AsciiFold` step that transliterates smart quotes, smart apostrophes, and dashes
  to ASCII equivalents and drops any remaining char >= 128. This satisfies the strict 7-bit
  ASCII player-facing output contract without a parallel sanitizer.
  (src/Tapestry.Engine/Recommend/PackRoomContext.cs;
  src/Tapestry.Authoring/RoomPromptBuilder.cs;
  src/Tapestry.Authoring/LlmRecommendProvider.cs)

### Oracle-frozen spawn sidecar

- A room side-car can carry a `spawns:` block: a list of entries with `base:` (mob template
  ID) and an optional `override:` sub-object (`from_type`, `name`, `desc`, `max_hp`,
  `damage`, `items`). This block round-trips through the YAML serializer so an
  oracle-minted room's rolled mobs survive a reload.
  (src/Tapestry.Engine/Authoring/RoomSpawnData.cs; src/Tapestry.Engine/Authoring/RoomData.cs)
- `RoomProjector` populates `RoomData.Spawns` from the area's registered `SpawnRule` entries
  when a `SpawnManager` is injected (optional; hand-built rooms without oracle spawns pass
  nothing and the field stays empty and is omitted from YAML).
  (src/Tapestry.Engine/Authoring/RoomProjector.cs)
- The serializer uses `OmitNull | OmitEmptyCollections` so the `spawns:` key is absent from
  YAML for rooms with no frozen spawns - preserving pack-room YAML cleanliness.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:40-44)

### Provenance classification

- Every area and room is classified as one of three provenance labels: `[pack]` (loaded
  from a pack, no side-car), `[authored]` (no source pack, side-car only), or
  `[pack +edits]` (pack-sourced with an overlaying side-car).
  (src/Tapestry.Engine/Authoring/ProvenanceClassifier.cs)

- `getAreaRooms(areaId)` returns each room's `{id, name, provenance}`. An authored room
  whose boundary connection could not be applied at boot - because the pack anchor room it
  hung off was absent from the world - has `(orphaned)` appended to its provenance string
  (e.g. `[authored] (orphaned)`). The check scans `ConnectionLoader.Dangling` (records that
  failed to apply, not `Loaded`), is gated on `source_pack == null` so pack rooms are never
  flagged, and surfaces in `rooms <area>` because that command renders the provenance
  verbatim. Detection is presence-based (any dangling record naming the room), not a
  reachability analysis. (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:GetAreaRooms;
  src/Tapestry.Scripting/Connections/ConnectionLoader.cs:Dangling)

### Room slug disambiguation

- On rename, `RoomSlugger.Slugify` lowercases the name, strips apostrophes, collapses
  non-alphanumeric runs to hyphens, trims leading/trailing hyphens, and drops a leading
  article (`the`, `a`, `an`). Names that produce no usable slug leave the id unchanged.
  Collisions are resolved by appending `-2`, `-3`, etc.
  (src/Tapestry.Engine/Authoring/RoomSlugger.cs)

### Stub exits in room YAML

- A room exit may be a stub (placeholder with no real target room). The YAML
  format for a stub exit is a mapping: `north: {stub: true, label: "a misty passage"}`.
  A real exit remains a bare scalar: `north: "oracle:origin"`. This is backward-
  compatible: the ExitDataConverter reads the legacy scalar form unchanged.
  (src/Tapestry.Engine/Authoring/ExitData.cs;
  src/Tapestry.Engine/Authoring/ExitDataConverter.cs;
  src/Tapestry.Scripting/YamlContentLoader.cs:ParseExit)

- `RoomData.Exits` is now `Dictionary<string, ExitData>` where `ExitData` carries
  `Target` (string), `Stub` (bool), and `Label` (string). Non-stub exits serialize
  as bare scalars (byte-identical to the legacy `Dictionary<string, string>` form).
  Stub exits serialize as a `{stub: true, label: "..."}` mapping.
  (src/Tapestry.Engine/Authoring/RoomData.cs)

- `RoomProjector.Project` emits `ExitData { Stub=true, Label=exit.DisplayName }` for
  stub exits and `ExitData { Target=exit.TargetRoomId }` for real exits. The neighbor
  projection loop naturally skips stubs because `_world.GetRoom("")` returns null.
  (src/Tapestry.Engine/Authoring/RoomProjector.cs)

- `WorldAuthoringModule.Serializer` registers `ExitDataConverter` so side-car writes
  emit stubs as mappings and real exits as bare scalars.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs)

- `AuthoredRoomLoader` skips the missing-target warning for stub exits
  (`!exit.IsStub` guard added). Stubs have an empty TargetRoomId by design.
  (src/Tapestry.Scripting/Authoring/AuthoredRoomLoader.cs)

- `tapestry.authoring.setStubExit(roomId, direction, label)` mints a stub exit on a
  runtime-authored (oracle-area) room and writes the side-car. Gated by `IsOracleArea`:
  only areas with no `SourcePack` (i.e. runtime-authored areas) accept stub exits.
  `tapestry.authoring.registerStubResolver(fn)` registers the JS resolver delegate
  on the singleton `StubExitResolver`. The JS function receives (roomId, dirStr) and
  must return a boolean.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:SetStubExit,IsOracleArea)

## Rejected and Reverted

- **Persisting runtime connection exits into side-cars or exported packs (TOMBSTONE):**
  Runtime connection exits -- exits created by the `link` command during a live session
  and stored as `ConnectionRecord` objects -- MUST NOT be written into area side-car files.
  When `WriteSideCar` serializes a room it strips all directional exits backed by a
  loaded connection record before writing, so connection exits do not appear in the file.
  Persisting them caused duplication on re-load and broken pack composition.
  (commit 3ddab86 "fix(scripting): side-car writes never persist connection-backed exits
  (composition leak)"; src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:412-462)

- **AuthoredRoomLoader parsing area.yaml files as rooms (TOMBSTONE):**
  The loader previously attempted to parse every YAML file under the data root, including
  area side-cars (`area.yaml`), as room definitions, causing load failures on authored
  area data. Fixed by filtering out filenames equal to `area.yaml` before processing.
  (commit 5904fdd "fix(engine): AuthoredRoomLoader skips area.yaml side-cars";
  src/Tapestry.Scripting/Authoring/AuthoredRoomLoader.cs:44-48)

## Change Log

- 2026-06-15 [extend-baked-in-areas](changes/2026-06-15-extend-baked-in-areas.md)
- 2026-06-22 [solo-oracle-e2-headless-recommend](changes/2026-06-22-solo-oracle-e2-headless-recommend.md)
- 2026-06-22 [solo-oracle-e1-frozen-spawn-override](changes/2026-06-22-solo-oracle-e1-frozen-spawn-override.md)
