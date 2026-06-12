---
capability: pack-security
last-updated: 2026-06-12
---

# Pack Security

## Overview

Pack security covers what the engine enforces at the pack boundary: JavaScript sandbox
constraints, which CLR surfaces are visible to pack scripts, the inter-pack dependency
gate, entity namespace enforcement, and the CI fixture isolation model that guarantees
seeded accounts cannot reach a published pack or a production image.

Registry collision policy (the seal, overrides, RegistrationPolicy) is covered in
registries-and-seal.md and is out of scope here.

## Behavior

### Jint sandbox limits

- The Jint engine is constructed with a 5-second execution timeout per top-level call,
  a recursion limit of 100 frames, and a memory cap of 50 MB per engine instance.
  (JintRuntime.cs:27-36)
- Strict mode is enabled for all scripts. (JintRuntime.cs:32)
- Mob behavior scripts carry an additional armable per-invocation wall-clock budget
  (MobInvocationBudget). The budget is disarmed by default (zero overhead for non-mob
  scripts) and armed around each mob AI invocation. A violation throws
  MobBudgetExceededException, which Jint propagates before the next constraint check.
  (MobInvocationBudget.cs:1-74)
- No direct CLR type access is granted to pack scripts. The engine exposes a single
  global object `tapestry` whose properties are the explicit module namespaces built by
  registered IJintApiModule implementations. All CLR delegates are wrapped as JS values;
  no AllowClr or equivalent general-purpose CLR bridge is enabled.
  (JintRuntime.cs:113-121; Tapestry.Scripting/ServiceCollectionExtensions.cs:36-199)

### API surface exposed to pack scripts

- All pack-facing APIs live under `tapestry.<namespace>` and are constructed from
  named IJintApiModule instances (JintRuntime.SetupApi). The full list of registered
  modules is in Tapestry.Scripting/ServiceCollectionExtensions.cs (lines 37-199). No module exposes
  raw engine internals; each wraps specific engine services.
- The `tapestry.fs` module (FsModule.cs) exposes `writeYaml` and `deleteFile`. Both
  operations are restricted to the server's `connections/` subdirectory by a path
  traversal check: the resolved absolute path must start with the resolved connections
  directory prefix, or the call throws. (FsModule.cs:54-63)
- The `tapestry.packs` module (PacksModule.cs) is the inter-pack call surface. A pack
  may only call, probe (`has`), or require another pack if it has declared an explicit
  dependency edge on that pack in its manifest. Self-calls are exempt. Violation throws
  InteropException at call time. (PacksModule.cs:255-267)

### Inter-pack dependency enforcement

- Pack dependency edges are built from `dependencies` and `optional_dependencies` in
  each pack manifest into a PackDependencyGraph. Both required and optional deps
  contribute edges. (PackDependencyGraph.cs:17-23)
- At boot, PackValidator.ValidateInteropCallSites resolves every literal-argument
  `tapestry.packs.call` and `tapestry.packs.has` site recorded during script load and
  confirms an edge exists for each. A missing edge is a fatal boot error. An absent
  optional dependency is skipped (expected has-guarded pattern). (PackValidator.cs:382-409)
- Cross-pack item `spawn_on` selectors are also edge-checked at boot: referencing a
  tag or id from another pack requires a declared dependency. (PackValidator.cs:191-218)

### Entity namespace enforcement

- Entity IDs are namespaced by pack. When a pack loads a room, mob, or item YAML file,
  `ValidateEntityId` checks that the ID's namespace prefix matches the current pack's
  namespace, throwing on mismatch. A duplicate ID across any two files is also fatal.
  (PackLoader.cs:487-505)

### Registration gate

- Once `PackLoader.LoadDeclarations` is called for the first boot pass, the
  RegistrationGate is armed. Any direct registry write outside a RegistrationPolicy
  commit scope then throws, preventing pack JS (or future pack scripts) from bypassing
  the policy by calling a raw C# registration function. The gate is disarmed by default
  so engine-internal C# boot registrations and unit tests are unaffected.
  (RegistrationGate.cs:1-53; PackLoader.cs:103-105)
- This wall was introduced to close the v0.1.20 mobs.registerCommand bypass (tapestry#98),
  where JS could write to a registry directly without going through the policy.
  (RegistrationGate.cs:7; PackLoader.cs:103)

### CI scenario fixture isolation

- Seeded test accounts (Gamemaster, Alice, Wanderer) and the test arena live exclusively
  in `@tapestry/test-fixtures` under `tests/fixtures/scenario-packs/`. The pack manifest
  includes the note "NEVER PUBLISH" and the description explicitly states the pack is
  structurally impossible to publish. (tests/fixtures/scenario-packs/@tapestry/test-fixtures/pack.yaml)
- The players.yaml in that pack carries plaintext test credentials and is annotated:
  "committed credentials are only tolerable because this file cannot reach a published
  pack or an image." (tests/fixtures/scenario-packs/@tapestry/test-fixtures/players.yaml:1-4)
- Physical separation is the enforcement mechanism: the packs publish workflow scans only
  the tapestry-packs registry path, and the engine image carries no packs at all
  (.dockerignore:7 excludes packs/; Dockerfile:9-15 copies only the publish output;
  commits 3cc3f75 and bf45105).
- The telnet runner stages the test-fixtures pack into a managed corpus at run time;
  it is never in the shipped pack list. (commit bf45105)

## Rejected and Reverted

- **Seeding user accounts in shipped packs (TOMBSTONE):** Packs MUST NOT ship with any
  seeded user accounts -- no admin accounts, backdoor logins, or default credentials of
  any kind. A default admin/password seed in a shipped pack was a security defect that was
  removed in the counterpart de-seed of tapestry-packs
  (core 0.1.12 / example-pack 0.1.8). Test credentials now live only in
  `@tapestry/test-fixtures`, which is structurally isolated from any published path.
  There is no legitimate use case for seeded accounts in a pack distributed via the
  registry or bundled with the engine. (commit bf45105)

## Change Log

| Date | Change Record | Summary |
|------|---------------|---------|
