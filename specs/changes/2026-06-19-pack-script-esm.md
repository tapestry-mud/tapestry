---
release: v0.1.40
specs: [scripting-runtime.md, pack-loading.md, events.md, flows-and-wizards.md]
---

# Pack Script ESM

## Why

All pack scripts ran in a single shared Jint global scope via `Execute`. Every pack
wrote into the same JS namespace, so a helper like `padRight` defined in one pack
silently clobbered the same name from another. Attribution -- knowing which pack
owned a running handler -- required explicit tracking: `__currentPack` and
`__currentSource` globals that `JintRuntime.Execute` set before each file body,
and `PackScope.InvokeAsPack` that saved/set/restored `__currentPack` around every
deferred callback (event handlers, schedules, flow callbacks, mob hooks, abilities).
Any dispatch path that missed the `InvokeAsPack` wrapper produced wrong attribution.

Cross-pack sharing required a parallel export/require system (`packs.export`,
`packs.require`, `packs.call`, `packs.has`, `RequireProxy`, `PackExportRegistry`,
`InteropCallSiteScanner`) distinct from ES module semantics, with its own edge
enforcement and static call-site scanning at boot.

The spike (D:\Skunkworks\jint-esm-spike, 7/7 passing) confirmed that Jint 4.9.3
supports native ES modules with a custom `ModuleLoader`. Each module gets its own
scope. Attribution becomes lexical: `Engine.GetActiveScriptOrModule()` (accessed via
a compiled IL delegate over the sealed internal API) tells you exactly which module
is executing right now, so no globals or wrappers are needed.

## What

- Pack scripts now load as native ES modules via `engine.Modules.Import(moduleKey)`.
  The canonical key is `pack:<ns>::<relFile>`. `PackLoader.LoadScripts` calls
  `JintRuntime.ImportModule` for each file instead of the old `Execute`.
- The engine API is an import, not a global. `JintRuntime.SetupApi` builds a single
  `@tapestry/engine` builder module with one named export per `IJintApiModule`
  namespace. Packs write `import * as tapestry from "@tapestry/engine"`.
- `TapestryModuleLoader` is the Jint `ModuleLoader`. It resolves `@tapestry/engine`
  to the builder module, relative specifiers within the importing pack, and bare
  scoped specifiers (`@scope/pack`) to the target pack's entry -- gating on a
  declared dependency edge (`PackDependencyGraph.DeclaresEdge`). A declared-optional-
  but-absent target resolves to an empty module so import-based capability guards work
  without throwing.
- `JintRuntime.GetActivePack()` reads the lexically active module via
  `JintActiveModule.ActiveLocation` (cached IL delegate over
  `Engine.GetActiveScriptOrModule`), then calls `TapestryModuleLoader.PackOf`. This
  replaces all `__currentPack` reads. `ScriptOwner.CurrentPackOwner/CurrentSourceFile`
  are the helpers used at registration-capture sites.
- The manifest flag `content.scripts_format` (C# property `PackContentPaths.ScriptsFormat`)
  must be `esm` (the only supported value; default). A non-esm value is a located boot
  error thrown by the guard in `PackLoader.LoadContent` before `LoadScripts` is called.
  `content.scripts` globs the compiled output (`dist/scripts/**/*.js`).
- Deleted: `PackScope`, `InvokeAsPack`, `__currentPack`, `__currentSource`, the
  `packs.export`/`require`/`call`/`has` JS surface, `RequireProxy`,
  `PackExportRegistry`, and `InteropCallSiteScanner`. `PackValidator` no longer runs
  the interop call-site validation pass. `tapestry.packs` is now introspection-only:
  `list()` and `getAll()`.
- Final strict boot: 9/9 packs, 0 issues, 0 validation errors (run by orchestrator
  post-Phase-J).
