---
capability: scripting-runtime
last-updated: 2026-06-12
---

# Scripting Runtime

## Overview

The scripting runtime hosts all pack JavaScript inside a single shared Jint engine instance.
Packs expose game logic through a curated `tapestry.*` API surface. The runtime provides pack
attribution, a deduplicated file execution ledger, an export/require system for inter-pack
value sharing, and a file-system module scoped to the connections directory.

Sandbox security constraints (timeouts, memory limits, strict mode, CLR bridge restrictions,
the RegistrationGate wall) are covered in pack-security.md and are NOT duplicated here. This
spec covers runtime mechanics only. Registration collision policy (RegistrationPolicy, seal,
override rules) is covered in registries-and-seal.md. Pack discovery, manifest format,
dependency graph, two-phase loading, script loading, and executed-file deduplication are
covered in pack-loading.md.

## Behavior

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
  `consumables`, `rest`, `doors`, `portals`, `area`, `time`, `weather`, `returnaddress`,
  `data`, `rarity`, `essence`, `stacking`, `packs`, `quests`, `watch`, `args`, `flows`,
  `schedule`, `gmcp`, `respond`, `notifications`, `connections`, `rooms`, `authoring`, `fs`

(ServiceCollectionExtensions.cs:37-199; each module's `Namespace` property)

Notes on two commonly misspelled names:
- `quests` (not `quest`) -- QuestModule.Namespace (QuestModule.cs:20)
- `returnaddress` (not `returnAddress`) -- ReturnAddressModule.Namespace (ReturnAddressModule.cs:11)

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
- `PackExportRegistry` keys entries by `(pack.ToLowerInvariant(), name)` -- the pack
  namespace half is lowercased for case-insensitive pack lookup, but the export name is
  case-sensitive. Collision and override legality is decided by `RegistrationPolicy` at the
  seal; the registry itself upserts. (PackExportRegistry.cs:27-28,34)
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

## Rejected and Reverted

(none for this scope -- see pack-loading.md for pack-loading tombstones)

## Change Log

| Date | Change Record | Summary |
|------|---------------|---------|

---

Sources consulted:
- src/Tapestry.Scripting/JintRuntime.cs
- src/Tapestry.Scripting/ServiceCollectionExtensions.cs
- src/Tapestry.Scripting/Interop/PackExportRegistry.cs (lines 27-28)
- src/Tapestry.Scripting/Interop/RequireProxy.cs
- src/Tapestry.Scripting/Interop/InteropCallSiteRegistry.cs
- src/Tapestry.Scripting/Interop/InteropCallSiteScanner.cs
- src/Tapestry.Scripting/Modules/PacksModule.cs
- src/Tapestry.Scripting/Modules/FsModule.cs
- src/Tapestry.Scripting/Modules/QuestModule.cs (Namespace property)
- src/Tapestry.Scripting/Modules/ReturnAddressModule.cs (Namespace property)
- src/Tapestry.Scripting/Modules/*.cs (all Namespace properties via grep)
- src/Tapestry.Scripting/PackContext.cs
- src/Tapestry.Scripting/PackScope.cs
- src/Tapestry.Scripting/LoadedPackNamespaces.cs
- src/Tapestry.Scripting/IJintApiModule.cs
- src/Tapestry.Scripting/PackValidator.cs
- src/Tapestry.Shared/PackManifest.cs
- git log --oneline (for 00-prefix tombstone verification)

UNVERIFIED count: 0
