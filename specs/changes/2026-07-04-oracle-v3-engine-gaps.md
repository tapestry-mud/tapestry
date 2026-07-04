---
release: 0.1.48
specs: [pack-loading.md, area-authoring.md, pack-security.md]
---

# Oracle V3 Engine Gaps

## Why

The oracle rooms v3 stage-A session documented three engine gaps it deliberately did
not patch (standing constraint: pack sessions do not touch the engine). All three cost
real debugging time or silently degraded output:

1. A runtime destination pack's generated content validated STRICT after reboot. The
   scaffold manifest saying `validation: lenient` is never read - docker deployments
   cannot write it (packs dir is read-only to the engine uid) and `server.yaml packs:`
   whitelists it out even when written - so any pack-declared property riding a
   generated room crashed the boot ("unregistered property oracle_populated" was the
   witnessed case, forcing the visited-marker workaround onto an oracle table).
2. A recommend request carrying a `response_format` json_schema while
   `llm.structured_output` was off got free text back with no signal: every pack mapper
   quietly fell back and the area generated with placeholder dressing.
3. The 5s Jint TimeoutInterval surfaced as a bare TimeoutException that read as
   arbitrary ReferenceErrors (a module import dying mid-evaluation leaves exports
   missing), costing an hour of misdirected debugging.

## What

- **Runtime namespaces validate lenient** (pack-loading.md, area-authoring.md).
  `RuntimeNamespaceStore` now remembers which namespaces are runtime-created - both
  `Register` (same session) and `LoadAtBoot` (marker restore) populate a runtime set
  exposed as `IsRuntimeNamespace`. `PackValidator` (new optional `RuntimeNamespaceStore`
  ctor param, DI-resolved) treats those namespaces as lenient in both the tag and
  property decision sites, logging one INFO per namespace instead of the strict-default
  warning. Manifest-less namespaces that are NOT runtime-created keep failing strict.
  Direction chosen over "honor the scaffold manifest" because the scaffold structurally
  cannot exist in docker; the data-dir marker already round-trips.
  (src/Tapestry.Scripting/RuntimeNamespaceStore.cs; src/Tapestry.Scripting/PackValidator.cs;
  tests/Tapestry.Scripting.Tests/PackValidatorRuntimeNamespaceTests.cs)

- **Schema-while-disabled is loud, not silent** (area-authoring.md).
  `LlmRecommendProvider` logs a WARN naming `llm.structured_output` when a request
  carries a response schema while the flag is off (throttled to one per 60s so a solo
  fill burst warns once) and increments the new `tapestry.recommend.schema_dropped`
  counter (tagged by field) on every dropped call. Behavior itself unchanged - the flag
  stays a deployment capability gate; no per-call auto-enable. Logger and metrics thread
  from the composition layer via optional `LlmProviderFactory.Create` /
  `BuildRecommendProvider` parameters.
  (src/Tapestry.Authoring/LlmRecommendProvider.cs; src/Tapestry.Engine/TapestryMetrics.cs;
  src/Tapestry.Scripting/ServiceCollectionExtensions.cs;
  tests/Tapestry.Engine.Tests/LlmRecommendProviderTests.cs)

- **Jint timeout says timeout** (pack-security.md). `ScriptTimeoutConstraint` replaces
  `options.TimeoutInterval(5s)` with identical budget/reset-per-entry semantics; a
  violation throws `ScriptTimeoutException` naming the budget and elapsed time and
  advising chunked work. Derives from TimeoutException so generic catch paths are
  unchanged. Pattern mirrors MobInvocationBudget.
  (src/Tapestry.Scripting/ScriptTimeoutConstraint.cs; src/Tapestry.Scripting/JintRuntime.cs;
  tests/Tapestry.Scripting.Tests/ScriptTimeoutConstraintTests.cs)
