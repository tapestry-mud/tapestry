---
capability: area-authoring
last-updated: 2026-07-04
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

- `AuthoredRoomLoader` then applies room side-cars: it scans `<root>/**/*.yaml` recursively
  but filters to files whose immediate parent directory is named `rooms`, and skips any file
  whose name ends in `-oracle-table.yaml`. This excludes item side-cars (`items/`), oracle
  table files, and the `area.yaml` root side-car from being parsed as rooms.
  (src/Tapestry.Scripting/Authoring/AuthoredRoomLoader.cs:44-53)

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

- `createPack(packName)` creates a runtime destination pack so generated content can be
  authored into a brand-new namespace: it registers the pack namespace into the live
  loaded-namespaces set (so a subsequent `createRoom` in that namespace is accepted) and
  persists it via `RuntimeNamespaceStore` to a `runtime-namespaces.txt` marker under the
  writable data directory, which `ContentLoadingModule` re-reads at boot so the namespace
  re-registers on reboot. It ALSO best-effort writes a minimal `type: world` `pack.yaml`
  scaffold under the packs root (so harvest has a real pack to fold in binary/local mode);
  that write is wrapped in try/catch because in the docker deployment the engine runs as a
  non-root uid and the packs dir is not writable by it - the data marker, owned by the
  engine, is the docker-safe reboot mechanism, and the generated content already persists
  as data side-cars loaded independently of the scaffold. Idempotent - if the namespace is
  already loaded it returns it without writing. Returns the registered namespace (scoped
  `@scope/name` maps to `scope-name`), or null for an empty name.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:CreatePack,
  src/Tapestry.Scripting/RuntimeNamespaceStore.cs)

- Runtime namespaces validate LENIENT, in-session and after reboot. The scaffold's
  `validation: lenient` never takes effect (docker cannot write it; `server.yaml packs:`
  whitelists it out even when written), so `RuntimeNamespaceStore` tracks the runtime set
  (`IsRuntimeNamespace`) across both `Register` and the boot `LoadAtBoot` restore, and
  `PackValidator` treats those namespaces as lenient for tag and property validation.
  Before this, restored generated content defaulted to strict and any pack-declared
  property riding a generated room crashed the boot ("unregistered property
  oracle_populated" was the witnessed case).
  (src/Tapestry.Scripting/RuntimeNamespaceStore.cs; src/Tapestry.Scripting/PackValidator.cs;
  tests/Tapestry.Scripting.Tests/PackValidatorRuntimeNamespaceTests.cs)

- Area mutators (`setAreaName`, `setAreaShort`, `setAreaDescription`, `setAreaTheme`,
  `setAreaLore`, `setAreaAttribute`) all update the live registry and rewrite the area
  side-car atomically. (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:518-576)

- `setAreaAttribute` accepts `level_range` (two-integer range), `reset_interval`
  (integer seconds), `wip` (bool), and `seed` (long integer). Other attribute names
  are rejected. (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:524-576)

- `area.yaml` carries a `seed:` field (long integer, omitted when zero). It is set
  via `setAreaAttribute(areaId, "seed", n)` and returned by `tapestry.area.get(areaId)`
  as `seed`. The seed is persisted so a shared or reloaded area replays as a pure
  function of its stored seed - the one unseeded creation roll survives restart and
  cross-session sharing. (src/Tapestry.Engine/AreaDefinition.cs;
  src/Tapestry.Scripting/YamlContentLoader.cs:SerializeAreaDefinition,LoadAreaDefinition;
  src/Tapestry.Scripting/Modules/AreaModule.cs)

- `tapestry.area.get(areaId)` projects `id`, `name`, `theme`, `levelRange`,
  `resetInterval`, `occupiedModifier`, `weatherZone`, `flags`, and `seed`. The `theme`
  field is the persisted area theme, so a reloaded area can reconstruct theme-derived
  state (e.g. generated room names) off disk without the in-memory creation context.
  (src/Tapestry.Scripting/Modules/AreaModule.cs)

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

### Structured LLM output

- The recommend call can return validated JSON instead of free text. When the caller
  supplies a stringified JSON Schema AND the server flag `llm.structured_output` is true,
  `OpenAiCompatibleLlmClient.CompleteAsync` attaches OpenAI structured outputs
  (`response_format: { type: "json_schema", json_schema: { name, schema, strict: true } }`)
  to the chat request and returns the raw JSON content string. The free-text
  `NormalizeSuggestion` step (whitespace-collapse + surrounding-quote-strip) is bypassed on
  this path because it corrupts JSON; ASCII folding happens pack-side on the parsed values.
  (src/Tapestry.Authoring/OpenAiCompatibleLlmClient.cs:23-60;
  src/Tapestry.Authoring/ILlmClient.cs:23;
  src/Tapestry.Authoring/LlmRecommendProvider.cs:53-70)

- `llm.structured_output` (C# `LlmSection.StructuredOutput` on `ServerConfig`) defaults to
  false and is mapped onto `RecommendLlmConfig.StructuredOutput` in the provider builder.
  Default-off means no deployment changes behavior without opt-in. With the flag off, or with
  no schema supplied, the provider runs the unchanged free-text path. A provider that ignores
  the schema (or the flag off) degrades safely to the caller's baked fallback - the pack-side
  JSON parse fails and the pack uses its own value.
  (src/Tapestry.Data/ServerConfig.cs:128;
  src/Tapestry.Scripting/ServiceCollectionExtensions.cs:259-270;
  src/Tapestry.Authoring/LlmRecommendProvider.cs:12-15)

- The schema-while-disabled fallback is loud, not silent. When a request carries a
  response schema while `llm.structured_output` is false, `LlmRecommendProvider` logs a
  WARN naming the flag (throttled to one per 60s so a multi-call solo fill burst warns
  once) and increments the `tapestry.recommend.schema_dropped` counter (tagged by field)
  on EVERY dropped call. The behavior itself is unchanged: the schema is not sent and
  free text comes back. Deliberately NOT auto-enabled per call - the flag is a deployment
  capability gate for providers that cannot do json_schema. The logger and metrics reach
  the provider from the composition layer through `LlmProviderFactory.Create` optional
  parameters. (src/Tapestry.Authoring/LlmRecommendProvider.cs:WarnSchemaDropped;
  src/Tapestry.Engine/TapestryMetrics.cs; src/Tapestry.Authoring/LlmProviderFactory.cs;
  src/Tapestry.Scripting/ServiceCollectionExtensions.cs;
  tests/Tapestry.Engine.Tests/LlmRecommendProviderTests.cs)

- Only strings cross the engine boundary on this seam: the pack passes a JSON-stringified
  schema in and the engine returns the model's content string (now JSON when a schema was
  supplied) out. The engine never learns the pack's data shapes.
  (src/Tapestry.Authoring/LlmRecommendProvider.cs:50-66)

### LLM token usage capture

- `ILlmClient.CompleteAsync` returns an `LlmResult` record (sanitized content + prompt and
  completion token counts) instead of a bare string. The provider parses the response `usage`
  block (counts are 0 when the provider reports no usage), and `RecommendResult` carries the
  token totals back to the binding. (src/Tapestry.Authoring/ILlmClient.cs:13,23;
  src/Tapestry.Authoring/OpenAiCompatibleLlmClient.cs:83-90;
  src/Tapestry.Engine/Recommend/IRecommendProvider.cs:8;
  src/Tapestry.Authoring/LlmRecommendProvider.cs:96-116)

- The recommend INFO log line carries a token field (e.g.
  `recommend[fill_mobs] ok 1731ms 293tok`), and a new histogram metric
  `tapestry.recommend.tokens` records prompt+completion tokens per call, tagged by field and
  outcome, alongside the existing recommend duration/calls metrics.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:235,245;
  src/Tapestry.Engine/TapestryMetrics.cs:134-137)

### Schema-aware stub provider

- When a recommend request carries a `ResponseSchema`, `StaticStubRecommendProvider` returns a
  valid JSON instance generated from the schema via `StubJson.FromSchema` (a minimal generator
  for object/array/string/number/integer/boolean + enum), so the structured-output path runs
  locally with no real LLM. The factory stub delay was lowered to 400ms (a solo fill run
  sequences several recommend calls, so the old 2000ms default blocked too long).
  (src/Tapestry.Engine/Recommend/StaticStubRecommendProvider.cs:31-34;
  src/Tapestry.Engine/Recommend/StubJson.cs:16-89;
  src/Tapestry.Authoring/LlmProviderFactory.cs:22-26)

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

- <!-- {B:authoring.writeItemTemplate} -->
  `tapestry.authoring.writeItemTemplate({ areaId, id, base, name, desc, type?, properties })`
  is an item-template sidecar writer parallel to `writeOracleTable`. It (1) looks up the
  base template by id in the live `ItemRegistry` (returns null if unknown), (2) merges the
  base template's keywords/tags/properties with the rolled `properties` overlay plus
  `description`, (3) **registers** the merged `ItemTemplate` under `id` in the live
  `ItemRegistry` so same-session mob inventory resolves without a reboot, and (4) writes
  the sidecar YAML via `YamlContentLoader.SerializeItemDefinition` to
  `<_root>/<areaId>/items/<shortId>.yaml` (where `shortId` is the segment after the last `:`
  in `id`). Path placement mirrors `WriteOracleTableSideCar`: `_root` is already
  `data/areas`, no `"areas"` literal added, `SafeSegment` applied to `areaId`. Nested
  all-numeric maps in `properties` (e.g. the `ac` damage-type map) are coerced to
  `Dictionary<string,int>` so `GetProperty<Dictionary<string,int>>("ac")` resolves on an
  exact-type match. Returns the registered `id` string, or null if the base is unknown.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:WriteItemTemplateSideCar,ItemTemplateSideCarPath)

- <!-- {B:authoring.writeOracleTable} -->
  `tapestry.authoring.writeOracleTable({ areaId, kind, entries })` is a table-sidecar
  writer parallel to the area/room sidecar writers. It (1) registers the table into the
  live `OracleTableRegistry` under `OracleTable.OracleTableId(areaId, kind)` (i.e.
  `<areaId>:<kind>`) so `tapestry.oracle.table(...)` resolves in the same solo run
  without a reboot, and (2) writes the sidecar YAML via `YamlContentLoader.SerializeOracleTable`.
  Path placement mirrors `AreaSideCarPath`/`SideCarPath` exactly: `_root` is already
  `data/areas`, no `"areas"` literal is added, and `SafeSegment` is applied to `areaId`.
  - `kind == "places"` -> `<_root>/<areaId>/places-oracle.yaml` (area-root, no subfolder)
  - all other kinds   -> `<_root>/<areaId>/<kind>/<singular>-oracle-table.yaml`
    where `singular` = kind with trailing `s` removed (e.g. `mobs` -> `mob-oracle-table.yaml`)
  `entries` is an array of `{ w, id, name?, desc?, balance_ref?, rarity? }` objects.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:WriteOracleTableSideCar,OracleTableSideCarPath)

## Rejected and Reverted

- **Persisting runtime connection exits into side-cars or exported packs (TOMBSTONE):**
  Runtime connection exits -- exits created by the `link` command during a live session
  and stored as `ConnectionRecord` objects -- MUST NOT be written into area side-car files.
  When `WriteSideCar` serializes a room it strips all directional exits backed by a
  loaded connection record before writing, so connection exits do not appear in the file.
  Persisting them caused duplication on re-load and broken pack composition.
  (commit 3ddab86 "fix(scripting): side-car writes never persist connection-backed exits
  (composition leak)"; src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:412-462)

- **AuthoredRoomLoader parsing non-room YAML files as rooms (TOMBSTONE):**
  The loader previously scanned all `*.yaml` files under the data root recursively without
  restricting by parent directory. This caused item side-cars (`items/*.yaml`), oracle table
  files (`*-oracle-table.yaml`), and area root files (`area.yaml`) to be parsed as rooms,
  triggering `PackValidator.ValidateProperties` crashes on boot (e.g. an item's `slot`
  property flagged as not applying to type=room). Fixed by: (1) filtering to files whose
  immediate parent directory is named `rooms`; (2) skipping files ending in
  `-oracle-table.yaml`. The old filename filter (`area.yaml`) is superseded by the
  parent-dir filter.
  (commit d1cb297 "fix(room-loader): exclude items/ and oracle-table files from the room scan";
  src/Tapestry.Scripting/Authoring/AuthoredRoomLoader.cs:44-53)

## Change Log

- 2026-06-28 [structured-llm-output](changes/2026-06-28-structured-llm-output.md) - recommend can return validated JSON via `response_format json_schema` (opt-in `llm.structured_output`, default off, degrades to baked fallback); `ILlmClient` returns `LlmResult` with token counts surfaced on the log line + new `tapestry.recommend.tokens` histogram; schema-aware `StaticStubRecommendProvider` via `StubJson.FromSchema`, stub delay lowered to 400ms
- 2026-06-27 [oracle-six-axis-overlay](changes/2026-06-27-oracle-six-axis-overlay.md) - `createPack` docker-safe: `RuntimeNamespaceStore` persists runtime namespaces to a writable `data/` marker + re-registers at boot; packs-dir scaffold write is now best-effort (try/catch)
- 2026-06-25 [solo-oracle-v2-completion](changes/2026-06-25-solo-oracle-v2-completion.md) - authoring.writeItemTemplate freeze seam; AuthoredRoomLoader scan hardened to rooms/ dir + skip oracle-table files
- 2026-06-22 [solo-oracle-e2-headless-recommend](changes/2026-06-22-solo-oracle-e2-headless-recommend.md)
- 2026-06-22 [solo-oracle-e1-frozen-spawn-override](changes/2026-06-22-solo-oracle-e1-frozen-spawn-override.md)
- 2026-06-15 [extend-baked-in-areas](changes/2026-06-15-extend-baked-in-areas.md)
