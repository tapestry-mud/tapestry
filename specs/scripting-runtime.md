---
capability: scripting-runtime
last-updated: 2026-06-26
---

# Scripting Runtime

## Overview

The scripting runtime hosts all pack JavaScript inside a single Jint engine instance.
Pack scripts load as native ES modules; the engine API is an import, not a global.
Attribution is lexical: the active module's identity is read at call time via
`JintActiveModule.ActiveLocation`, so every handler knows which pack owns it without
any explicit pack-name threading.

Sandbox security constraints (timeouts, memory limits, strict mode, CLR bridge restrictions,
the RegistrationGate wall) are covered in pack-security.md and are NOT duplicated here. This
spec covers runtime mechanics only. Registration collision policy (RegistrationPolicy, seal,
override rules) is covered in registries-and-seal.md. Pack discovery, manifest format,
dependency graph, two-phase loading, script loading, and executed-file deduplication are
covered in pack-loading.md.

## Behavior

### IJintApiModule -- module self-registration

- `IJintApiModule` exposes two members: `string Namespace { get; }` and
  `object Build(JintEngine engine)`. (src/Tapestry.Scripting/IJintApiModule.cs:1-9)
- `JintRuntime.SetupApi` builds one object per module. For each registered module it
  captures `module.Build(engine)` under `module.Namespace`. All built objects are then
  exported as named exports on a single shared `@tapestry/engine` builder module registered
  via `engine.Modules.Add`. (src/Tapestry.Scripting/JintRuntime.cs:114-141)
- All modules are registered as `IJintApiModule` singletons in DI; JintRuntime receives
  them via `IEnumerable<IJintApiModule>` constructor injection. (src/Tapestry.Scripting/ServiceCollectionExtensions.cs:36-100;
  src/Tapestry.Scripting/JintRuntime.cs:24-25)

### tapestry.* API surface (namespaces via import)

Pack scripts import the engine API as a named-export module:

```js
import * as tapestry from "@tapestry/engine";
```

The following namespaces are available as named exports on `@tapestry/engine`:

  `commands`, `emotes`, `events`, `world`, `stats`, `inventory`, `equipment`, `items`,
  `combat`, `progression`, `mobs`, `theme`, `dice`, `abilities`, `effects`, `classes`,
  `races`, `alignment`, `ui`, `help`, `training`, `admin`, `currency`, `shop`,
  `consumables`, `rest`, `doors`, `portals`, `area`, `time`, `weather`, `returnaddress`,
  `data`, `rarity`, `essence`, `stacking`, `packs`, `quests`, `watch`, `args`, `flows`,
  `schedule`, `gmcp`, `respond`, `notifications`, `connections`, `rooms`, `authoring`, `fs`,
  `registry`, `oracle`

(src/Tapestry.Scripting/ServiceCollectionExtensions.cs:37-199; each module's `Namespace` property)

Notes on two commonly misspelled names:
- `quests` (not `quest`) -- QuestModule.Namespace (src/Tapestry.Scripting/Modules/QuestModule.cs:20)
- `returnaddress` (not `returnAddress`) -- ReturnAddressModule.Namespace (src/Tapestry.Scripting/Modules/ReturnAddressModule.cs:11)

### ESM loading -- pack scripts as native ES modules

- Each pack script file is imported via `engine.Modules.Import(moduleKey)` using the
  canonical key `pack:<ns>::<relFile>`. This replaces the old shared-global `Execute` path.
  (src/Tapestry.Scripting/JintRuntime.cs:97; src/Tapestry.Scripting/PackLoader.cs:544)
- `JintRuntime` enables the module loader by passing a `TapestryModuleLoader` to
  `options.EnableModules(loader)` at construction. (src/Tapestry.Scripting/JintRuntime.cs:40-43)
- ESM is the only supported loader. `content.scripts_format` (C# property
  `PackContentPaths.ScriptsFormat`, default `"esm"`) must be `"esm"`; any other value
  is a located boot error (guard in `PackLoader.LoadContent` before `LoadScripts`).
  (src/Tapestry.Shared/PackManifest.cs:39-40; src/Tapestry.Scripting/PackLoader.cs)
- `content.scripts` globs the compiled output (typically `dist/scripts/**/*.js`).
  (src/Tapestry.Shared/PackManifest.cs:36)

### TapestryModuleLoader -- resolver and cross-pack gate

- `TapestryModuleLoader` is the Jint `ModuleLoader` for all pack imports. It handles three
  specifier kinds:
  - `@tapestry/engine` - resolves to the shared builder module.
  - Relative (`./` or `../`) - resolves within the importing pack's directory.
  - Bare scoped (`@scope/pack`) - resolves to the target pack's entry (`dist/scripts/index.js`),
    gating on a declared dependency edge. (src/Tapestry.Scripting/Interop/TapestryModuleLoader.cs:81-137)
- A declared-optional-but-absent target resolves to an empty module (`export {};`) so a
  `import * as ns from "@scope/pack"` + capability-guard pattern sees no exports rather than
  throwing. (src/Tapestry.Scripting/Interop/TapestryModuleLoader.cs:127-131)
- Cross-pack imports without a declared dependency edge throw `InvalidOperationException`
  at resolve time (boot error). This is the sole cross-pack gate; the legacy
  `packs.export`/`require`/`call`/`has` and `RequireProxy`/`PackExportRegistry` surface is
  deleted. (src/Tapestry.Scripting/Interop/TapestryModuleLoader.cs:116-135)
- `TapestryModuleLoader.PackOf(location)` extracts the pack namespace from a canonical
  module key string (`pack:<ns>::<relFile>`). (src/Tapestry.Scripting/Interop/TapestryModuleLoader.cs:60-69)
- `TapestryModuleLoader.SourceOf(location)` extracts the relative file path from the same
  key. (src/Tapestry.Scripting/Interop/TapestryModuleLoader.cs:71-79)
- `TapestryModuleLoader` is built at boot via `Build()` / `BuildFromManifests()` with the
  pack-directory map and optional-edge set, then registered in DI.
  (src/Tapestry.Scripting/Interop/TapestryModuleLoader.cs:31-56;
  src/Tapestry.Scripting/ServiceCollectionExtensions.cs:98-99)

### GetActivePack -- lexical pack attribution

- `JintRuntime.GetActivePack()` reads the currently executing ES module's location via
  `JintActiveModule.ActiveLocation(_engine)`, then calls
  `TapestryModuleLoader.PackOf(location)` to extract the pack namespace. Attribution is
  lexical: it reflects the module that is currently on the call stack, not any stored global.
  (src/Tapestry.Scripting/JintRuntime.cs:101-102)
- `JintActiveModule.ActiveLocation` uses a compiled IL delegate to call Jint's internal
  `Engine.GetActiveScriptOrModule()` (bypassing the sealed `internal` visibility in the
  pinned 4.9.3 build), then reads the `Location` property off the returned object via a
  cached `PropertyInfo`. (src/Tapestry.Scripting/Modules/JintActiveModule.cs:25-63)
- `ScriptOwner.CurrentPackOwner(engine)` and `ScriptOwner.CurrentSourceFile(engine)` are
  extension helpers that compose `JintActiveModule.ActiveLocation` +
  `TapestryModuleLoader.PackOf/SourceOf` for registration-capture sites.
  (src/Tapestry.Scripting/ScriptOwner.cs:12-17)
- Deferred handlers (event subscribers, schedules, flows, mob hooks, abilities) self-
  attribute at invocation time via the same `GetActivePack()` path -- the active module is
  the pack's ES module when the handler runs. No `__currentPack` global, `InvokeAsPack`,
  or `PackScope` exists. (src/Tapestry.Scripting/ScriptOwner.cs:7-9)

### packs surface -- introspection only

- `tapestry.packs` exposes two methods: `list()` and `getAll()`, both returning the array
  of loaded pack metadata in `load_order` order. The legacy export/require/call/has
  surface is deleted; cross-pack sharing uses native ES module import/export gated by the
  `TapestryModuleLoader` resolver. (src/Tapestry.Scripting/Modules/PacksModule.cs:26-31)

### FsModule -- file API

- `tapestry.fs` exposes two methods: `writeYaml(relativePath, data)` and
  `deleteFile(relativePath)`. Both are restricted to the server's `connections/` subdirectory;
  the resolved absolute path must start with the resolved connections-dir prefix or the call
  throws. (src/Tapestry.Scripting/Modules/FsModule.cs:31-63)
- `writeYaml` serialises the JS value to YAML using `UnderscoredNamingConvention` with
  null-omission, then writes to `<serverRoot>/connections/<relativePath>`, creating the
  directory if absent. (src/Tapestry.Scripting/Modules/FsModule.cs:31-41)
- `deleteFile` deletes `<serverRoot>/connections/<relativePath>` if it exists; no error if
  absent. (src/Tapestry.Scripting/Modules/FsModule.cs:43-49)
- `FsModule` is constructed with `config.ConfigDirectory` as the server root path, injected
  by the DI factory in `ServiceCollectionExtensions`. (src/Tapestry.Scripting/ServiceCollectionExtensions.cs:193-198;
  src/Tapestry.Scripting/Modules/FsModule.cs:21-23)

### LoadedPackNamespaces -- live namespace set

- `LoadedPackNamespaces` holds a single case-insensitive `HashSet<string>` populated by
  `PackLoader.LoadDeclarations` as each active pack is declared. Modules (e.g.
  `WorldAuthoringModule`) that need the loaded set hold the SAME mutable instance so that
  late population (packs finishing load after the module was constructed) is visible without
  a snapshot. (src/Tapestry.Scripting/LoadedPackNamespaces.cs:1-26; src/Tapestry.Scripting/ServiceCollectionExtensions.cs:152-153)

### RegistryModule -- registry introspection surface

- `RegistryModule` exposes the registration policy's post-seal readers to pack JS as
  `tapestry.registry.summary()` (per-kind counts + conflict counts),
  `tapestry.registry.list(kind, name?)` (the ledger for a kind, optionally one name),
  and `tapestry.registry.conflicts()` (only names with more than one claimant, across
  all kinds). (src/Tapestry.Scripting/Modules/RegistryModule.cs:22-37)
- The two namespaced registries (property, tag) bypass the policy by design, so the
  module adapts them in HERE rather than inside `RegistrationPolicy`: it reads their
  `GetAll()` and normalizes each into the same row shape the policy kinds return,
  tagging the model `policy` or `namespaced` so a renderer does not branch. Namespaced
  rows carry ambiguity (a bare name declared by two or more packs) where policy rows
  carry shadow/override. (src/Tapestry.Scripting/Modules/RegistryModule.cs:40-169)

### Swell combat seam (`tapestry.combat.registerWindow`)

- `tapestry.combat.registerWindow(name, fn)` registers a deterministic window
  validator through the seal; the engine invokes it at swell resolve with a
  read-only `CombatContext` and expects a `ValidationResult`. The validator is
  pack JS, never the model. (src/Tapestry.Scripting/Modules/CombatModule.cs:259;
  src/Tapestry.Engine/Combat/WindowValidatorRegistry.cs:8)
- `commands.register` marshals the optional `pace: 'free' | 'battle'` field off
  the command object (default `free`), rejecting any other value, and threads it
  to `CommandRegistration.Pace`.
  (src/Tapestry.Scripting/Modules/CommandsModule.cs:134)

### authoring.recommend -- headless LLM binding

- `authoring.recommend(options, callback)` kicks off a non-blocking LLM call and
  delivers the result via `callback(result)` on the game loop thread. Fire-and-forget;
  returns nothing. The callback receives a sanitized ASCII string or `null` on
  disabled/timeout/failure.
- `options` shape: `{ field: string, roomId?: string, template: string, system?: string, vars?: { [k]: string } }`.
  - `field`: the recommend field (e.g. `"description"`, `"name"`).
  - `roomId`: when set, the engine projects that room's context (neighbors, area, biome)
    as structural grounding for the prompt. Omit when no room exists yet.
  - `template`: pack-owned task line with `{varName}` placeholders (vars substituted before sending).
  - `system`: optional pack-owned system voice; falls back to the engine default when omitted.
  - `vars`: optional key-value map substituted into `template`.
- The engine projects neighbor-aware context via `RoomProjector` + `RoomPromptBuilder`
  (same 1-hop neighbor name/biome/description stitching used by the interactive builder
  recommend flow). The pack supplies the task line and system voice; engine code is touched
  only to project context, never to author wording.
- The async call rides `RecommendBroker.RecommendAsync` off the loop thread, then
  `ContinueWith -> GameLoop.Schedule(Action)` so the JS callback runs on the loop thread.
  This is the same mechanism network threads use to post work onto the loop.
- The binding double-guards `recommendEnabled`: if the broker is disabled, not present,
  or `options` is not a JS object, it schedules `callback(null)` immediately and returns.
- Output is sanitized via the existing `OutputSanitizer.Clean` + `NormalizeSuggestion`
  path, extended with an `AsciiFold` step that transliterates smart quotes/dashes and
  drops any remaining char >= 128, enforcing the strict 7-bit ASCII contract.
- `authoring.recommendEnabled()` (existing) is the pack-side pre-check; the binding
  double-guards so a pack that skips the check still degrades gracefully.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs;
  src/Tapestry.Engine/Recommend/PackRoomContext.cs;
  src/Tapestry.Authoring/RoomPromptBuilder.cs;
  src/Tapestry.Authoring/LlmRecommendProvider.cs)

### tapestry.consequence -- in-memory room consequence overlay

- `tapestry.consequence.stamp(roomId, kind, lifespan)` records a consequence on a room;
  idempotent on (roomId, kind), last lifespan wins; returns `true` on success. Kind and
  lifespan are opaque strings defined by pack content (e.g. `"looted"`, `"ephemeral"`).
  (src/Tapestry.Scripting/Modules/ConsequenceModule.cs)
- `tapestry.consequence.list(roomId)` returns an array of `{ kind, lifespan }` objects for
  all consequences stamped on the room; empty array if none.
  (src/Tapestry.Scripting/Modules/ConsequenceModule.cs)
- `tapestry.consequence.has(roomId, kind)` returns `true` if the given kind is stamped on
  the room. (src/Tapestry.Scripting/Modules/ConsequenceModule.cs)
- `tapestry.consequence.clear(roomId)` removes all consequences for the room; returns `true`
  if anything was removed. (src/Tapestry.Scripting/Modules/ConsequenceModule.cs)
- Backing store is `ConsequenceOverlay` (src/Tapestry.Engine/Consequence/ConsequenceOverlay.cs),
  a memory-only singleton. Ephemeral entries are evicted on the `area.tick` event (the repop
  cadence); persistent and succession-seed entries survive until reboot. The overlay resets
  on engine restart by construction - no disk state.

### tapestry.oracle -- read-only oracle table binding

- `tapestry.oracle.table(id)` looks up a registered `OracleTable` by id and returns a
  plain JS object `{ id, kind, entries: [{ w, id, name, desc, balance_ref, rarity, extra }] }`,
  or `null` when the id is not registered. The binding is read-only; no mutation surface is
  exposed. (src/Tapestry.Scripting/Modules/OracleModule.cs)
- Rolling is the pack's responsibility. The engine binding exposes the frozen entry list so
  the pack's seeded PRNG (e.g. `splitmix64`) can perform deterministic weighted selection
  on any box without engine involvement.
- `OracleModule` is registered as an `IJintApiModule` singleton in
  `Tapestry.Scripting.ServiceCollectionExtensions`, receiving `OracleTableRegistry` via
  constructor injection. (src/Tapestry.Scripting/ServiceCollectionExtensions.cs)

## Rejected and Reverted

- **Shared-global Execute realm (TOMBSTONE):** Before Phase J, pack scripts ran through
  `JintRuntime.Execute`, which set `__currentPack` and `__currentSource` JS globals before
  Jint executed each file body. Deferred handlers were dispatched through `PackScope.InvokeAsPack`,
  which saved/set/restored `__currentPack` around each call. The shared realm meant all packs
  shared one JS global scope, which caused name collision bugs (e.g., the `padRight` global
  collision between admin and groups scripts). Phase J replaced this with native ES modules:
  each pack module has its own scope; attribution is lexical via `GetActivePack()`; cross-pack
  sharing is native import/export gated by the resolver. `PackScope`, `__currentPack`,
  `__currentSource`, `InvokeAsPack`, `PackExportRegistry`, `RequireProxy`, and
  `InteropCallSiteScanner` are all deleted.

## Change Log

- 2026-06-23 [solo-oracle-v2-seams](changes/2026-06-23-solo-oracle-v2-seams.md) - T3: `tapestry.oracle.table(id)` read-only binding via `OracleModule`; rolling stays in pack JS
- 2026-06-22 [solo-oracle-engine-seams](changes/2026-06-22-solo-oracle-engine-seams.md) - E2 pack recommend context: headless `authoring.recommend` callable without a builder session; engine-projected room context via `RoomProjector`/`PackRoomContext`; ASCII-fold sanitization
- 2026-06-22 [solo-oracle-e2-headless-recommend](changes/2026-06-22-solo-oracle-e2-headless-recommend.md) - `authoring.recommend(options, callback)` non-blocking headless LLM binding; engine-projected context + pack-owned template; ASCII-fold sanitization
- 2026-06-21 [boss-combat-slice-1](changes/2026-06-21-boss-combat-slice-1.md) - `tapestry.combat.registerWindow` validator seam and `pace` marshalling in `commands.register`
- 2026-06-19 [pack-script-esm](changes/2026-06-19-pack-script-esm.md) - Native ESM loader, `@tapestry/engine` import, lexical GetActivePack attribution; legacy shared-global realm, PackScope, InvokeAsPack, packs.export/require/call/has, RequireProxy, PackExportRegistry, InteropCallSiteScanner deleted
- 2026-06-19 [registry-introspection](changes/2026-06-19-registry-introspection.md) - `RegistryModule` (`tapestry.registry.*`), the JS interop surface over the policy readers, adapting the property/tag outliers in at this layer
