---
capability: pack-loading
last-updated: 2026-06-12
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
  (e.g. `legends-forgotten/`) are added directly; scoped scope directories (e.g. `@mallek/`)
  are descended into and their children added, also alphabetically.
  (ContentLoadingModule.cs:184-206)
- If `packs:` in `server.yaml` is non-empty, discovered directories are filtered to only
  those matching one of three forms (case-insensitive): the scoped name
  (`@mallek/legends-forgotten`), the namespace form (`mallek-legends-forgotten`), or the bare
  folder name (`legends-forgotten`). All three are checked against the relative path from the
  packs directory. (ContentLoadingModule.cs:138-149)
- If `packs:` is empty or absent, all discovered directories are loaded.
- The declarations phase (Phase 1) runs in the alphabetical discovery order produced by
  `DiscoverPackDirectories`. The content phase (Phase 2) runs in dependency-topological order
  (see below). (ContentLoadingModule.cs:152-179)

### pack.yaml manifest format

- Each pack directory must contain either `pack.yaml` or `tapestry.yaml`; if neither exists,
  `PackLoader.LoadDeclarations` throws `FileNotFoundException`. (PackLoader.cs:112-120)
- Manifests are deserialised via `YamlContentLoader.LoadManifest` using YamlDotNet with
  `UnderscoredNamingConvention` and `IgnoreUnmatchedProperties`. (YamlContentLoader.cs:30-34,
  68-71)
- `name` and `version` are NOT required by the engine. Both have defaults (`""` and `"1.0.0"`
  respectively) and are not validated for presence; the loader will not throw on a manifest
  that omits them. (PackManifest.cs:5-6)
- `name` follows npm-style scoped syntax: `@scope/pack-name` or bare `pack-name`. Namespace
  form is derived by stripping `@`, replacing `/` with `-` (e.g. `@tapestry/core` ->
  `tapestry-core`). (PackLoader.cs:93-97)
- `version` is a semver string stored for display/introspection only; the engine performs no
  version enforcement. (PackManifest.cs:6)
- **engine: is a no-op.** Manifest files commonly write `engine: ">=0.1.29"` for
  documentation, but the YAML key `engine` does not bind to any property. The C# property
  that stores the engine constraint is `EngineVersion`, which binds to the snake_case key
  `engine_version`. `IgnoreUnmatchedProperties` silently drops `engine:` at load time, so
  engine-version pinning via the `engine:` key has no runtime effect.
  (PackManifest.cs:13; YamlContentLoader.cs:33)
- `dependencies` is a `string -> string` map of `pack-name: version-constraint`. Only
  presence is checked at boot; version ranges are not resolved by the engine.
  (PackManifest.cs:15; PackValidator.cs:354-366)
- `optional_dependencies` uses the snake_case key `optional_dependencies`. Absent optional
  deps are not a boot error; they contribute edges to the dependency graph for runtime gate
  purposes. (PackManifest.cs:19; PackManifestOptionalDepsTests.cs:9-39)
- `active: false` skips the pack entirely at both declaration and content phases.
  (PackLoader.cs:128-131)
- `load_order` is an integer used for display sort via `tapestry.packs.list()`; it does NOT
  control load sequence (dependency graph does). (PackManifest.cs:20; PacksModule.cs:316)
- `validation: lenient` downgrades unknown tag/property errors to warnings for that pack;
  default is `strict`. (PackManifest.cs:21; PackValidator.cs:271-302)
- `content:` sub-keys are all glob patterns relative to the pack directory. Recognised keys:
  `area_definitions`, `rooms`, `items`, `mobs`, `scripts`, `strings`, `equipment_slots`,
  `weather_zones`, `help`, `quests`, `motd`, `motd_color`. (PackContentPaths.cs via
  PackManifest.cs:28-45)
- The first pack to declare a non-empty `content.motd` file path wins; subsequent packs'
  motd declarations are silently ignored. (PackLoader.cs:140-163)

### PackDependencyGraph and topological load order

- `PackDependencyGraph.Build` accepts a map of `namespace -> [dep namespaces]` where each
  pack's dep list is the union of `dependencies` and `optional_dependencies` in namespace
  form. (PackDependencyGraph.cs:17-23)
- `TopologicalOrder` implements Kahn's algorithm. Ties (no ordering constraint between packs)
  break alphabetically for determinism. (PackDependencyGraph.cs:42-93)
- Edges pointing at packs not in the loaded set are silently ignored, enabling optional deps.
  (PackDependencyGraph.cs:47-55)
- If a dependency cycle is detected (no pack is satisfiable), the algorithm breaks the cycle
  by emitting the alphabetically smallest un-emitted pack; the result always contains every
  loaded pack exactly once. (PackDependencyGraph.cs:73-76)
- `PackDependencyGraph` implements `IPackEdgeOracle` and is registered as both its concrete
  type and that interface, so engine systems that only need edge queries receive the same
  singleton. (ServiceCollectionExtensions.cs:95-96)

### Two-phase loading: declarations then content

- `PackLoader.Load` calls `LoadDeclarations` then `LoadContent` in sequence. The server boot
  calls these in two separate passes over all packs (declarations first for all packs, content
  second), allowing tags and properties declared by one pack to be visible when a later pack
  loads its content. (PackLoader.cs:265-270; TwoPhaseLoadingTests.cs:9-16)
- `LoadDeclarations` reads the manifest, records the pack namespace in `LoadedPackNamespaces`,
  and loads tags, properties, and equipment slots. The declarations pass runs in alphabetical
  discovery order. (PackLoader.cs:99-175; ContentLoadingModule.cs:152-153)
- `LoadContent` runs weather zones, area definitions, rooms, items, fixtures, themes, mobs,
  scripts, help, and quests in that fixed order. The content pass runs in dependency-
  topological order. (PackLoader.cs:177-244; ContentLoadingModule.cs:159-179)

### Script loading and init.js priority

- The `content.scripts` glob is resolved via `Microsoft.Extensions.FileSystemGlobbing.Matcher`
  and files are sorted alphabetically before execution. (PackLoader.cs:646-652)
- If `init.js` is present anywhere in the glob results, it is executed first regardless of
  alphabetical position; all other matched files follow. (PackLoader.cs:543-553)
- Each script is executed with `JintRuntime.Execute(text, packName, relativeFile)`, which
  sets the `__currentPack` and `__currentSource` globals before Jint executes the body.
  (JintRuntime.cs:49-54; PackLoader.cs:558-562)
- `ScheduleModule.ResetPack` is called before the first script of each pack, clearing any
  scheduled callbacks registered by a previous hot-reload of the same pack. (PackLoader.cs:541)

### Executed-file deduplication ledger

- `JintRuntime.MarkFileExecuted(absolutePath)` records paths in a case-insensitive
  `HashSet<string>` using `Path.GetFullPath` normalisation. Returns `true` on first sight,
  `false` on repeat. The ledger survives the full boot. (JintRuntime.cs:82-85)
- PackLoader calls `MarkFileExecuted` for every script file executed via the `scripts:` glob.
  `QuestScriptCoverageVerifier` (QuestStartupModule) reads `HasExecutedFile` to confirm every
  quest `script:` file was covered by the glob. Removing the ledger call breaks this check.
  (JintRuntime.cs:70-85; PackLoader.cs:551,563)
- The deduplication guard prevents a file listed in both the `scripts:` glob and a quest
  `script:` field from executing twice and triggering a registration collision.
  (JintRuntime.cs:15-20)

### PackValidator -- content validation summary

- `PackValidator.Validate` checks in order: mob configs, item configs, room configs, tags,
  properties, dependency presence, interop call sites. (PackValidator.cs:59-71)
- Missing required dependencies are always fatal (no lenient downgrade).
  (PackValidator.cs:349-366)
- Unknown tags/properties on entities owned by a lenient pack are logged as warnings; on
  strict packs they throw. (PackValidator.cs:271-302; PackValidator.cs:320-345)

## Rejected and Reverted

- **Dual script execution (TOMBSTONE):** Before commit f0d8f14, the `scripts:` glob and the
  quest `script:` field were independent boot subsystems that both executed JS files. A file
  covered by both would run twice, re-firing every `registerX` call and silently clobbering
  registries under the old last-write-wins model. The executed-file deduplication ledger
  (`_executedFiles`) and `QuestScriptCoverageVerifier` close this gap: the glob runs first
  and marks files executed; the quest loader confirms coverage rather than re-executing.
  (JintRuntime.cs:15-20; commit f0d8f14)

## Change Log

| Date | Change Record | Summary |
|------|---------------|---------|
