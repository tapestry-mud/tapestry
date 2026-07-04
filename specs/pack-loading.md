---
capability: pack-loading
last-updated: 2026-07-04
---

# Pack Loading

## Overview

The pack loading subsystem discovers packs from `server.yaml`, reads `pack.yaml` manifests,
resolves a dependency-topological load order, and drives two sequential phases (declarations
then content) so that tags and properties registered by one pack are visible when a later
pack loads its content. Script loading, init.js priority, and executed-file deduplication are
the final steps of the content phase.

Sandbox security constraints and the RegistrationGate wall are covered in pack-security.md.
Registration collision policy and the seal barrier are covered in registries-and-seal.md.
The `tapestry.*` API surface exposed to scripts is covered in scripting-runtime.md.

## Behavior

### server.yaml -- pack discovery and filtering

- `DiscoverPackDirectories` enumerates the packs directory alphabetically. Non-scoped entries
  (e.g. `world-pack/`) are added directly; scoped scope directories (e.g. `@acme/`)
  are descended into and their children added, also alphabetically.
  (src/Tapestry.Server/Modules/ContentLoadingModule.cs:184-206)
- If `packs:` in `server.yaml` is non-empty, discovered directories are filtered to only
  those matching one of three forms (case-insensitive): the scoped name
  (`@acme/world-pack`), the namespace form (`acme-world-pack`), or the bare
  folder name (`world-pack`). All three are checked against the relative path from the
  packs directory. (src/Tapestry.Server/Modules/ContentLoadingModule.cs:138-149)
- If `packs:` is empty or absent, all discovered directories are loaded.
- The declarations phase (Phase 1) runs in the alphabetical discovery order produced by
  `DiscoverPackDirectories`. The content phase (Phase 2) runs in dependency-topological order
  (see below). (src/Tapestry.Server/Modules/ContentLoadingModule.cs:152-179)

### pack.yaml manifest format

- Each pack directory must contain either `pack.yaml` or `tapestry.yaml`; if neither exists,
  `PackLoader.LoadDeclarations` throws `FileNotFoundException`. (src/Tapestry.Scripting/PackLoader.cs:112-120)
- Manifests are deserialised via `YamlContentLoader.LoadManifest` using YamlDotNet with
  `UnderscoredNamingConvention` and `IgnoreUnmatchedProperties`. (src/Tapestry.Scripting/YamlContentLoader.cs:30-34,
  68-71)
- `name` and `version` are NOT required by the engine. Both have defaults (`""` and `"1.0.0"`
  respectively) and are not validated for presence; the loader will not throw on a manifest
  that omits them. (src/Tapestry.Shared/PackManifest.cs:5-6)
- `name` follows npm-style scoped syntax: `@scope/pack-name` or bare `pack-name`. Namespace
  form is derived by stripping `@`, replacing `/` with `-` (e.g. `@tapestry/core` ->
  `tapestry-core`). (src/Tapestry.Scripting/PackLoader.cs:93-97)
- `version` is a semver string stored for display/introspection only; the engine performs no
  version enforcement. (src/Tapestry.Shared/PackManifest.cs:6)
- **engine: is a no-op.** Manifest files commonly write `engine: ">=0.1.29"` for
  documentation, but the YAML key `engine` does not bind to any property. The C# property
  that stores the engine constraint is `EngineVersion`, which binds to the snake_case key
  `engine_version`. `IgnoreUnmatchedProperties` silently drops `engine:` at load time, so
  engine-version pinning via the `engine:` key has no runtime effect.
  (src/Tapestry.Shared/PackManifest.cs:13; src/Tapestry.Scripting/YamlContentLoader.cs:33)
- `dependencies` is a `string -> string` map of `pack-name: version-constraint`. Only
  presence is checked at boot; version ranges are not resolved by the engine.
  (src/Tapestry.Shared/PackManifest.cs:15; src/Tapestry.Scripting/PackValidator.cs:354-366)
- `optional_dependencies` uses the snake_case key `optional_dependencies`. Absent optional
  deps are not a boot error; they contribute edges to the dependency graph for runtime gate
  purposes. (src/Tapestry.Shared/PackManifest.cs:19; tests/Tapestry.Scripting.Tests/PackManifestOptionalDepsTests.cs:9-39)
- `active: false` skips the pack entirely at both declaration and content phases.
  (src/Tapestry.Scripting/PackLoader.cs:128-131)
- `load_order` is an integer used for display sort via `tapestry.packs.list()`; it does NOT
  control load sequence (dependency graph does). (src/Tapestry.Shared/PackManifest.cs:20; src/Tapestry.Scripting/Modules/PacksModule.cs:316)
- `validation: lenient` downgrades unknown tag/property errors to warnings for that pack;
  default is `strict`. (src/Tapestry.Shared/PackManifest.cs:21; src/Tapestry.Scripting/PackValidator.cs:271-302)
- `content:` sub-keys are all glob patterns relative to the pack directory. Recognised keys:
  `area_definitions`, `oracle`, `rooms`, `items`, `mobs`, `scripts`, `strings`, `equipment_slots`,
  `weather_zones`, `help`, `quests`, `motd`, `motd_color`. (src/Tapestry.Shared/PackManifest.cs:29-46)
- The first pack to declare a non-empty `content.motd` file path wins; subsequent packs'
  motd declarations are silently ignored. (src/Tapestry.Scripting/PackLoader.cs:140-163)

### PackDependencyGraph and topological load order

- `PackDependencyGraph.Build` accepts a map of `namespace -> [dep namespaces]` where each
  pack's dep list is the union of `dependencies` and `optional_dependencies` in namespace
  form. (src/Tapestry.Scripting/Interop/PackDependencyGraph.cs:17-23)
- `TopologicalOrder` implements Kahn's algorithm. Ties (no ordering constraint between packs)
  break alphabetically for determinism. (src/Tapestry.Scripting/Interop/PackDependencyGraph.cs:42-93)
- Edges pointing at packs not in the loaded set are silently ignored, enabling optional deps.
  (src/Tapestry.Scripting/Interop/PackDependencyGraph.cs:47-55)
- If a dependency cycle is detected (no pack is satisfiable), the algorithm breaks the cycle
  by emitting the alphabetically smallest un-emitted pack; the result always contains every
  loaded pack exactly once. (src/Tapestry.Scripting/Interop/PackDependencyGraph.cs:73-76)
- `PackDependencyGraph` implements `IPackEdgeOracle` and is registered as both its concrete
  type and that interface, so engine systems that only need edge queries receive the same
  singleton. (src/Tapestry.Scripting/ServiceCollectionExtensions.cs:95-96)

### Two-phase loading: declarations then content

- `PackLoader.Load` calls `LoadDeclarations` then `LoadContent` in sequence. The server boot
  calls these in two separate passes over all packs (declarations first for all packs, content
  second), allowing tags and properties declared by one pack to be visible when a later pack
  loads its content. (src/Tapestry.Scripting/PackLoader.cs:265-270; tests/Tapestry.Scripting.Tests/TwoPhaseLoadingTests.cs:9-16)
- `LoadDeclarations` reads the manifest, records the pack namespace in `LoadedPackNamespaces`,
  and loads tags, properties, and equipment slots. The declarations pass runs in alphabetical
  discovery order. (src/Tapestry.Scripting/PackLoader.cs:99-175; src/Tapestry.Server/Modules/ContentLoadingModule.cs:152-153)
- `LoadContent` runs weather zones, area definitions, rooms, items, fixtures, themes, mobs,
  scripts, help, and quests in that fixed order. The content pass runs in dependency-
  topological order. (src/Tapestry.Scripting/PackLoader.cs:177-244; src/Tapestry.Server/Modules/ContentLoadingModule.cs:159-179)

### Oracle table content kind

- A pack declares its oracle tables via the `oracle:` glob in `pack.yaml` (e.g.
  `oracle: "areas/**/*-oracle-table.yaml"`). The snake_case key maps to `PackContentPaths.Oracle`
  via `UnderscoredNamingConvention`. (src/Tapestry.Shared/PackManifest.cs)
- `PackLoader.LoadContent` dispatches to `LoadOracleData` when `manifest.Content.Oracle` is
  non-empty, mirroring the `LoadAreaDefinitions` pattern: glob -> deserialize -> register.
  (src/Tapestry.Scripting/PackLoader.cs)
- Each matched file is deserialized via `YamlContentLoader.LoadOracleTable`, which reads the
  `oracle_table:` envelope and returns an `OracleTable` with a `Kind` field and weighted
  `Entries`. (src/Tapestry.Scripting/YamlContentLoader.cs)
- The canonical table id is `<area-folder>:<kind>`, derived by:
  1. Extracting the area folder name from the file path segment immediately after `areas/`
     (e.g. `areas/castle-kitchen/mobs/mob-oracle-table.yaml` -> `"castle-kitchen"`).
  2. Combining with `table.Kind` via `OracleTable.OracleTableId(areaId, kind)` ->
     `$"{areaId}:{kind}"` (e.g. `"castle-kitchen:mobs"`).
  This is the ONE canonical id used by the loader (T2), the same-session register (T4),
  the authored-table loader (T6), and the pack resolver (P-C). (src/Tapestry.Shared/OracleTable.cs)
- The resolved id is stored on `table.Id` before calling `OracleTableRegistry.Register`.
  `table.SourcePack` is set to the pack namespace. (src/Tapestry.Scripting/PackLoader.cs)
- `OracleTableRegistry` is a singleton (registered in `AddTapestryEngine`) resolved by DI as
  a `PackLoader` constructor parameter. (src/Tapestry.Engine/ServiceCollectionExtensions.cs;
  tests/Tapestry.Engine.Tests/OracleTableLoadTests.cs:PackLoader_loads_oracle_tables_from_glob_and_keys_by_area_and_kind)

### AuthoredOracleLoader -- boot reload of frozen tables

- `AuthoredOracleLoader` scans the authoring root (`config.Persistence.RoomsPath`, i.e.
  `data/areas`) for oracle table side-car files at boot and registers them into
  `OracleTableRegistry`. This is the reload complement to T4 (same-session live register):
  T4 keeps tables live during the current session; `AuthoredOracleLoader` restores them after
  a reboot. (src/Tapestry.Scripting/Authoring/AuthoredOracleLoader.cs)
- Matched files: any `*.yaml` file whose name ends in `-oracle-table.yaml` (e.g.
  `mobs/mob-oracle-table.yaml`, `items/item-oracle-table.yaml`) OR equals `places-oracle.yaml`
  (case-insensitive). All other YAML files (e.g. `area.yaml`, room files) are skipped.
  (src/Tapestry.Scripting/Authoring/AuthoredOracleLoader.cs)
- Area id extraction: the first path segment after the root, e.g.
  `data/areas/castle-kitchen/mobs/mob-oracle-table.yaml` -> `"castle-kitchen"`.
  This mirrors the path `WorldAuthoringModule.OracleTableSideCarPath` writes.
  (src/Tapestry.Scripting/Authoring/AuthoredOracleLoader.cs)
- The canonical id is `OracleTable.OracleTableId(areaId, table.Kind)` = `"<areaId>:<kind>"`,
  identical to the key used by the pack loader (T2), the same-session register (T4), and the
  resolver (P-C). `table.SourcePack` is set to the area id (not a pack namespace).
  (src/Tapestry.Shared/OracleTable.cs)
- The `pack oracle:` glob covers only tables shipped inside a pack directory (baked sets).
  Tables frozen to the authoring root via `authoring.writeOracleTable` are NOT under any pack
  dir, so the pack glob never reaches them; `AuthoredOracleLoader` is the only reload path.
- `AuthoredOracleLoader` is registered as a singleton in `Tapestry.Scripting/ServiceCollectionExtensions.cs`
  (same pattern as `AuthoredAreaLoader` and `AuthoredRoomLoader`), wired to the same
  `config.Persistence.RoomsPath` root. `ContentLoadingModule.Configure` calls `.Load()` after
  `AuthoredAreaLoader` and `AuthoredRoomLoader`.
  (src/Tapestry.Scripting/ServiceCollectionExtensions.cs; src/Tapestry.Server/Modules/ContentLoadingModule.cs)
- Missing root: if the authoring root does not exist, `Load()` returns immediately with no error.
  (src/Tapestry.Scripting/Authoring/AuthoredOracleLoader.cs)
- Bad file: a file that fails to parse is logged as a warning and skipped; the rest of the scan
  continues. (src/Tapestry.Scripting/Authoring/AuthoredOracleLoader.cs)

### AuthoredItemLoader -- boot reload of frozen item side-cars

- <!-- {B:AuthoredItemLoader} -->
  `AuthoredItemLoader` scans the authoring root (`config.Persistence.RoomsPath`, i.e. `data/areas`)
  for item-template side-car files at boot and registers them into `ItemRegistry`. This is the reload
  complement to `authoring.writeItemTemplate` (A1): `writeItemTemplate` keeps a frozen item live
  during the current session; `AuthoredItemLoader` restores it after a reboot so a re-spawned mob's
  `override.items` list still resolves.
  (src/Tapestry.Scripting/Authoring/AuthoredItemLoader.cs)
- Matched files: any `*.yaml` file whose containing directory is named `items` (case-insensitive).
  All other YAML files are skipped. (src/Tapestry.Scripting/Authoring/AuthoredItemLoader.cs)
- Each file is loaded via `YamlContentLoader.LoadItem(yaml, propertyRegistry)` - the
  `PropertyRegistry` is REQUIRED so that the nested `ac` map coerces to `Dictionary<string,int>`;
  passing null returns a raw object map and worn armor reads 0 AC silently. Mirror how
  `PackLoader.LoadItems` always passes `_propertyRegistry`.
  (src/Tapestry.Scripting/Authoring/AuthoredItemLoader.cs; src/Tapestry.Scripting/YamlContentLoader.cs)
- The `ItemDefinition` is mapped to `ItemTemplate` identically to `PackLoader.LoadItems`:
  `Id`, `Name`, `Type`, `Tags`, `Keywords`, `Properties` (with `(object?)` cast). The item's own
  `id:` field is the registration key; no path-segment derivation is needed (unlike oracle tables).
  (src/Tapestry.Scripting/Authoring/AuthoredItemLoader.cs)
- `AuthoredItemLoader` is registered as a singleton in `Tapestry.Scripting/ServiceCollectionExtensions.cs`
  (same pattern as `AuthoredOracleLoader`), wired to the same `config.Persistence.RoomsPath` root.
  `ContentLoadingModule.Configure` calls `.Load()` right after `_authoredOracleLoader.Load()`.
  (src/Tapestry.Scripting/ServiceCollectionExtensions.cs; src/Tapestry.Server/Modules/ContentLoadingModule.cs)
- Missing root: if the authoring root does not exist, `Load()` returns immediately with no error.
- Bad file: a file that fails to parse is logged as a warning and skipped; the rest of the scan
  continues. (src/Tapestry.Scripting/Authoring/AuthoredItemLoader.cs)

### Script loading and init.js priority

- The `content.scripts` glob is resolved via `Microsoft.Extensions.FileSystemGlobbing.Matcher`
  and files are sorted alphabetically before execution. (src/Tapestry.Scripting/PackLoader.cs:631-636)
- If `init.js` is present anywhere in the glob results, it is imported first regardless of
  alphabetical position; all other matched files follow. (src/Tapestry.Scripting/PackLoader.cs:548-553)
- Each script is imported as a native ES module via `JintRuntime.ImportModule(moduleKey)`,
  where the key is `pack:<packName>::<relFile>`. Attribution is lexical through the active
  module -- no `__currentPack` or `__currentSource` globals are set.
  (src/Tapestry.Scripting/JintRuntime.cs:97; src/Tapestry.Scripting/PackLoader.cs:540-546)
- `ScheduleModule.ResetPack` is called before the first script of each pack, clearing any
  scheduled callbacks registered by a previous hot-reload of the same pack. (src/Tapestry.Scripting/PackLoader.cs:538)

### Executed-file deduplication ledger

- `JintRuntime.MarkFileExecuted(absolutePath)` records paths in a case-insensitive
  `HashSet<string>` using `Path.GetFullPath` normalisation. Returns `true` on first sight,
  `false` on repeat. The ledger survives the full boot. (src/Tapestry.Scripting/JintRuntime.cs:82-85)
- PackLoader calls `MarkFileExecuted` for every script file executed via the `scripts:` glob.
  `QuestScriptCoverageVerifier` (src/Tapestry.Server/Modules/QuestStartupModule.cs) reads `HasExecutedFile` to confirm every
  quest `script:` file was covered by the glob. Removing the ledger call breaks this check.
  (src/Tapestry.Scripting/JintRuntime.cs:70-85; src/Tapestry.Scripting/PackLoader.cs:551,563)
- The deduplication guard prevents a file listed in both the `scripts:` glob and a quest
  `script:` field from executing twice and triggering a registration collision.
  (src/Tapestry.Scripting/JintRuntime.cs:15-20)

### PackValidator -- content validation summary

- `PackValidator.Validate` checks in order: mob configs, item configs, room configs, tags,
  properties, dependency presence. (src/Tapestry.Scripting/PackValidator.cs:56-63)
- Missing required dependencies are always fatal (no lenient downgrade).
  (src/Tapestry.Scripting/PackValidator.cs:345-359)
- Unknown tags/properties on entities owned by a lenient pack are logged as warnings; on
  strict packs they throw. (src/Tapestry.Scripting/PackValidator.cs:271-302; src/Tapestry.Scripting/PackValidator.cs:320-345)
- The strict/lenient decision is keyed off the owning pack's loaded manifest
  (`validation:` key), with two manifest-less fallbacks: a namespace registered or
  boot-restored by `RuntimeNamespaceStore` (a runtime-created destination pack; its
  content is engine-written side-cars reloaded by the Authored*Loaders) validates
  LENIENT; any other manifest-less namespace (e.g. side-cars lingering from a pack
  removed from `server.yaml`) logs a warning and validates strict.
  (src/Tapestry.Scripting/PackValidator.cs; src/Tapestry.Scripting/RuntimeNamespaceStore.cs;
  tests/Tapestry.Scripting.Tests/PackValidatorRuntimeNamespaceTests.cs)

## Rejected and Reverted

- **Dual script execution (TOMBSTONE):** Before commit f0d8f14, the `scripts:` glob and the
  quest `script:` field were independent boot subsystems that both executed JS files. A file
  covered by both would run twice, re-firing every `registerX` call and silently clobbering
  registries under the old last-write-wins model. The executed-file deduplication ledger
  (`_executedFiles`) and `QuestScriptCoverageVerifier` close this gap: the glob runs first
  and marks files executed; the quest loader confirms coverage rather than re-executing.
  (src/Tapestry.Scripting/JintRuntime.cs:15-20; commit f0d8f14)

## Change Log

- 2026-06-25 [solo-oracle-v2-completion](changes/2026-06-25-solo-oracle-v2-completion.md) - AuthoredItemLoader boot/reload scan for frozen item side-cars; mirrors AuthoredOracleLoader; PropertyRegistry required for ac map coercion
- 2026-06-23 [solo-oracle-v2-seams](changes/2026-06-23-solo-oracle-v2-seams.md) - T1: `OracleTableRegistry` DI singleton sealed with other registries; T2: `oracle:` content glob routes to `LoadOracleData` (`<areaFolder>:<kind>` id); T6: `AuthoredOracleLoader` boot scanner for `*-oracle-table.yaml` in the authoring root
- 2026-06-19 [pack-script-esm](changes/2026-06-19-pack-script-esm.md) - Script loading now imports each file as a native ES module via ImportModule; attribution is lexical (no __currentPack globals); PackValidator no longer validates interop call sites (deleted)
