---
capability: scripting-runtime
last-updated: 2026-06-12
---

# Scripting Runtime

## Overview

The scripting runtime hosts all pack JavaScript inside a single shared Jint engine instance.
Packs are discovered from `server.yaml`, loaded in dependency-topological order, and expose
game logic through a curated `tapestry.*` API surface. The runtime provides pack attribution,
a deduplicated file execution ledger, an export/require system for inter-pack value sharing,
and a file-system module scoped to the connections directory.

Sandbox security constraints (timeouts, memory limits, strict mode, CLR bridge restrictions,
the RegistrationGate wall) are covered in pack-security.md and are NOT duplicated here. This
spec covers runtime mechanics only. Registration collision policy (RegistrationPolicy, seal,
override rules) is covered in registries-and-seal.md.

## Behavior

### server.yaml -- pack discovery

- The `packs:` list in `server.yaml` is the authoritative pack list. Each entry is a
  directory name relative to the server root. Packs are loaded in the order needed by their
  declared dependency graph, not necessarily the order listed. (server.yaml:9-11)

### pack.yaml manifest format

- Each pack directory must contain either `pack.yaml` or `tapestry.yaml`; if neither exists,
  `PackLoader.LoadDeclarations` throws `FileNotFoundException`. (PackLoader.cs:112-120)
- Manifest fields deserialised via YamlDotNet `UnderscoredNamingConvention`. Required: `name`,
  `version`. Optional but expected: `display_name`, `description`, `author`, `license`,
  `engine`, `active`, `load_order`, `validation`. (PackManifest.cs:1-27;
  YamlContentLoader.cs:68-71)
- `name` follows npm-style scoped syntax: `@scope/pack-name` or bare `pack-name`. Namespace
  form is derived by stripping `@`, replacing `/` with `-` (e.g. `@tapestry/core` ->
  `tapestry-core`). (PackLoader.cs:93-97)
- `version` is a semver string (no engine enforcement -- stored for display/introspection
  only). (PackManifest.cs:6)
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

### Dependency graph and topological load order

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

- `PackLoader.Load` calls `LoadDeclarations` then `LoadContent` in sequence. Server boot
  calls these in two separate passes over all packs (declarations first for all packs, content
  second), allowing tags and properties declared by one pack to be visible when a later pack
  loads its content. (PackLoader.cs:265-270; TwoPhaseLoadingTests.cs:9-16)
- `LoadDeclarations` reads the manifest, records the pack namespace in `LoadedPackNamespaces`,
  and loads tags, properties, and equipment slots. (PackLoader.cs:99-175)
- `LoadContent` runs weather zones, area definitions, rooms, items, fixtures, themes, mobs,
  scripts, help, and quests in that fixed order. (PackLoader.cs:177-244)

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

### IJintApiModule -- module self-registration

- `IJintApiModule` exposes two members: `string Namespace { get; }` and
  `object Build(JintEngine engine)`. (IJintApiModule.cs:1-9)
- `JintRuntime.SetupApi` builds one `ExpandoObject` as the `tapestry` global. For each
  registered module it writes `tapestry[module.Namespace] = module.Build(engine)`.
  (JintRuntime.cs:113-121)
- All modules are registered as `IJintApiModule` singletons in DI; JintRuntime receives
  them via `IEnumerable<IJintApiModule>` constructor injection. (ServiceCollectionExtensions.cs:36-90;
  JintRuntime.cs:22-24)

### tapestry.* API surface (namespaces)

The following namespaces are available to pack scripts as `tapestry.<namespace>`:

  `commands`, `emotes`, `events`, `world`, `stats`, `inventory`, `equipment`, `items`,
  `combat`, `progression`, `mobs`, `theme`, `dice`, `abilities`, `effects`, `classes`,
  `races`, `alignment`, `ui`, `help`, `training`, `admin`, `currency`, `shop`,
  `consumables`, `rest`, `doors`, `portals`, `area`, `time`, `weather`, `returnAddress`,
  `data`, `rarity`, `essence`, `stacking`, `packs`, `quest`, `watch`, `args`, `flows`,
  `schedule`, `gmcp`, `respond`, `notifications`, `connections`, `rooms`, `authoring`, `fs`

(ServiceCollectionExtensions.cs:37-199; each module's `Namespace` property)

### PackContext -- current-pack attribution

- `PackContext` holds `CurrentPackDir` and `CurrentPackNamespace` as mutable strings.
  PackLoader updates these before each content-loading step. (PackContext.cs:1-7;
  PackLoader.cs:111,186)
- `PackScope.InvokeAsPack` saves the current `__currentPack` global, sets it to the
  handler-owner's pack name, runs the invocation, then restores the previous value. All
  registered handlers (commands, events, schedules, abilities, flows) are dispatched through
  this scope so attribution is correct at call time, not load time. (PackScope.cs:14-44)

### Export/require system

- A pack exports a value via `tapestry.packs.export(name, handler, metadata)`. The export
  name must match the JS identifier pattern `^[a-zA-Z_$][a-zA-Z0-9_$]*$`. Exporting a
  non-function, non-object value throws `InteropException`. (PacksModule.cs:96-147;
  PacksModule.cs:17)
- Function exports are invoked via `tapestry.packs.call(pack, name, ...args)`, which
  enforces a declared dependency edge and runs the handler attributed to the exporting pack.
  Namespace/data exports are accessed via `tapestry.packs.require(pack)` which returns a
  `RequireProxy`. (PacksModule.cs:149-224)
- `tapestry.packs.has(pack)` returns true if the pack is loaded; `tapestry.packs.has(pack,
  name)` also checks that the export exists. Both enforce the dependency edge.
  (PacksModule.cs:227-238)
- `tapestry.packs.require(pack)` returns a `RequireProxy` whose member access resolves
  against `PackExportRegistry` at USE time (not at require-call time). This allows a pack to
  obtain a proxy before its dependency has exported. Function members are wrapped to run
  attributed to the exporting pack. Namespace/data members are returned as-is.
  (RequireProxy.cs:15-20; RequireProxy.cs:44-78)
- The proxy is read-only; assignment or property definition throws `InteropException`.
  Enumeration yields no keys; use `tapestry.packs.getExportRegistry()` for introspection.
  (RequireProxy.cs:85-95; RequireProxy.cs:19-20)
- `PackExportRegistry` keys entries by `(pack, name)` lowercased. Collision and override
  legality is decided by `RegistrationPolicy` at the seal; the registry itself upserts.
  (PackExportRegistry.cs:27-37)
- Exports registered before the seal are eagerly visible to load-time interop calls (safe
  because dependency-ordered loading ensures the exporter runs first). (PacksModule.cs:138-146)

### InteropCallSite static scanning

- During script loading, `InteropCallSiteScanner.Extract` parses the source with Acornima
  and walks the AST to record every `tapestry.packs.call`, `tapestry.packs.has`, and
  `tapestry.packs.require` site whose pack and export arguments are string literals. Dynamic-
  dispatch sites are not recorded. (InteropCallSiteScanner.cs:16-101)
- Recorded sites accumulate in `InteropCallSiteRegistry` for the duration of the boot.
  `PackValidator.ValidateInteropCallSites` drains the registry at seal time to perform static
  interop resolution: edge present, target loaded, export exists (for `call`).
  (InteropCallSiteRegistry.cs:1-17; PackValidator.cs:382-409)
- A parse failure in `InteropCallSiteScanner` yields zero sites; Jint's subsequent
  `Execute` will surface the canonical syntax error. (InteropCallSiteScanner.cs:22-29)

### FsModule -- file API

- `tapestry.fs` exposes two methods: `writeYaml(relativePath, data)` and
  `deleteFile(relativePath)`. Both are restricted to the server's `connections/` subdirectory;
  the resolved absolute path must start with the resolved connections-dir prefix or the call
  throws. (FsModule.cs:31-63)
- `writeYaml` serialises the JS value to YAML using `UnderscoredNamingConvention` with
  null-omission, then writes to `<serverRoot>/connections/<relativePath>`, creating the
  directory if absent. (FsModule.cs:31-41)
- `deleteFile` deletes `<serverRoot>/connections/<relativePath>` if it exists; no error if
  absent. (FsModule.cs:43-49)
- `FsModule` is constructed with `config.ConfigDirectory` as the server root path, injected
  by the DI factory in `ServiceCollectionExtensions`. (ServiceCollectionExtensions.cs:193-198;
  FsModule.cs:21-23)

### LoadedPackNamespaces -- live namespace set

- `LoadedPackNamespaces` holds a single case-insensitive `HashSet<string>` populated by
  `PackLoader.LoadDeclarations` as each active pack is declared. Modules (e.g.
  `WorldAuthoringModule`) that need the loaded set hold the SAME mutable instance so that
  late population (packs finishing load after the module was constructed) is visible without
  a snapshot. (LoadedPackNamespaces.cs:1-26; ServiceCollectionExtensions.cs:152-153)

### PackValidator -- content validation

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
- **00- prefix load ordering (removed):** Early packs used `00-` filename prefixes to control
  script execution order. The dependency-topological load sequence and `init.js` priority
  replace this pattern; the prefix is no longer respected. (See registries-and-seal.md)

## Change Log

| Date | Change Record | Summary |
|------|---------------|---------|

---

Sources consulted:
- src/Tapestry.Scripting/JintRuntime.cs
- src/Tapestry.Scripting/PackLoader.cs
- src/Tapestry.Scripting/PackValidator.cs
- src/Tapestry.Scripting/ServiceCollectionExtensions.cs
- src/Tapestry.Scripting/Interop/PackDependencyGraph.cs
- src/Tapestry.Scripting/Interop/PackExportRegistry.cs
- src/Tapestry.Scripting/Interop/RequireProxy.cs
- src/Tapestry.Scripting/Interop/InteropCallSite.cs
- src/Tapestry.Scripting/Interop/InteropCallSiteRegistry.cs
- src/Tapestry.Scripting/Interop/InteropCallSiteScanner.cs
- src/Tapestry.Scripting/Modules/PacksModule.cs
- src/Tapestry.Scripting/Modules/FsModule.cs
- src/Tapestry.Scripting/PackContext.cs
- src/Tapestry.Scripting/PackScope.cs
- src/Tapestry.Scripting/LoadedPackNamespaces.cs
- src/Tapestry.Scripting/IJintApiModule.cs
- src/Tapestry.Scripting/YamlContentLoader.cs (lines 29-71)
- src/Tapestry.Shared/PackManifest.cs
- server.yaml
- tests/fixtures/packs/example-pack/tapestry.yaml
- tests/Tapestry.Scripting.Tests/PackLoaderTests.cs
- tests/Tapestry.Scripting.Tests/JintRuntimeTests.cs
- tests/Tapestry.Scripting.Tests/PackManifestOptionalDepsTests.cs
- tests/Tapestry.Scripting.Tests/TwoPhaseLoadingTests.cs
- tests/Tapestry.Scripting.Tests/Interop/InteropCallSiteRegistryTests.cs
- git log --oneline -15 -- src/Tapestry.Scripting/PackLoader.cs src/Tapestry.Scripting/JintRuntime.cs

UNVERIFIED count: 0

Out-of-scope notes:
- Jint engine limits (timeout 5s, recursion 100, memory 50MB, strict mode, MobInvocationBudget) -- covered in pack-security.md
- RegistrationGate and the wall against out-of-policy registry writes -- covered in pack-security.md
- Registration collision policy, override rules, seal barrier -- covered in registries-and-seal.md
- Entity namespace enforcement (ValidateEntityId) -- covered in pack-security.md
- Cross-pack spawn_on edge checks -- covered in pack-security.md
- CI fixture isolation (test-fixtures pack) -- covered in pack-security.md
- The server.yaml pack-list ordering relative to dependency resolution: server.yaml lists
  packs by directory name, but actual load sequence is controlled by the dependency graph
  (TopologicalOrder); the exact mechanism by which the Server reads server.yaml and feeds
  pack directories to PackLoader was not read for this spec (Tapestry.Server entry point
  not consulted).
