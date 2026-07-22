---
capability: registries-and-seal
last-updated: 2026-06-19
---

# Registries and Seal

## Overview

All pack JavaScript runs inside the engine seal. Packs declare registrations during the
accumulation phase; the seal resolves conflicts and commits winners at boot. The registration
policy enforces that same-name entries from different packs are either an error or a
declared override.

## Behavior

- **Seal barrier:** Pack code runs inside the JavaScript engine (Jint). The seal exposes a
  curated API surface; pack code cannot access CLR types directly. No AllowClr or equivalent
  general-purpose CLR bridge is enabled.
  (src/Tapestry.Scripting/JintRuntime.cs:27-37,113-121)

- **Declarative accumulation:** All `tapestry.<registry>.register(...)` calls during the
  pack load phase record a `RegistrationCandidate` (fields: Kind, Name, Owner, IsOverride,
  Commit, plus SourceFile/Line for located diagnostics). The seal barrier calls
  `RegistrationPolicy.Resolve()` once at the end of boot;
  it groups candidates by `(Kind, Name)`, picks order-independent winners, and replays each
  winner's Commit.
  (src/Tapestry.Engine/Registration/RegistrationPolicy.cs:8-15,84-101)

- **Collision policy:** A same-name registration from two packs is a strict boot error
  unless exactly one pack declares `{ override: true }` with a valid dependency edge on the
  owner pack. `kernel` and `engine` owners are non-overridable.
  (src/Tapestry.Engine/Registration/RegistrationPolicy.cs:32-33,122-180)

- **Post-seal registration:** The policy supports sanctioned runtime registrations
  after the seal closes. New names bind immediately; a duplicate of a sealed winner without
  a declared override throws. This path is used internally; pack authors should not rely on
  it.
  (src/Tapestry.Engine/Registration/RegistrationPolicy.cs:48-73)

- **Export/require surface:** Packs may export values and require exports from other packs
  via the seal's export surface. Cross-pack sharing uses this path, not ambient globals.
  (src/Tapestry.Scripting/Modules/PacksModule.cs:41-224)

- **JsValue[] marshalling:** CLR method parameters typed as `JsValue[]` do not correctly
  receive JavaScript array arguments under Jint 4.x. Take a single `JsValue` parameter and
  unpack inside the method body. This applies to any CLR-side method exposed to pack JS.
  (src/Tapestry.Scripting/Modules/PacksModule.cs:149-166;
  tests/Tapestry.Scripting.Tests/Modules/PacksInteropModuleTests.cs:234-259)

- **Post-seal introspection (read-only):** `RegistrationPolicy` exposes two reader
  methods, safe to call after the seal: `GetRegistrySummary()` returns one
  `RegistrySummaryRow` per kind (name count + shadow-conflict count), and
  `GetRegistrations(kind, name?)` returns `RegistryEntryView` records for a kind -
  each marked winner/override, an override winner naming the base it shadows and a
  shadowed loser naming the winner. Both records are flat and primitive-only by
  design. The methods only read the accumulated ledger; they do not register,
  resolve, or mutate.
  (src/Tapestry.Engine/Registration/RegistrationPolicy.cs:103-170)

- **OracleTableRegistry:** A content registry (not seal-gated; like `AreaRegistry`, registered
  directly by `PackLoader`). Keys are `<area-slug>:<kind>` where `area-slug` is the area folder
  name (unique authoring root key), so two areas' tables of the same kind never collide
  (e.g. `castle-kitchen:mobs`). The canonical key is produced by `OracleTable.OracleTableId(areaId, kind)`.
  Holds the frozen oracle tables a pack reads at runtime. Registered as a singleton in
  `ServiceCollectionExtensions`.
  (src/Tapestry.Engine/OracleTableRegistry.cs;
  src/Tapestry.Shared/OracleTable.cs;
  tests/Tapestry.Engine.Tests/OracleTableLoadTests.cs)

### Scoped removal

Three registries expose a removal path, added for solo-area teardown:

- `AreaRegistry.Unregister(id)` removes one area definition and returns whether it existed.
- `OracleTableRegistry.RemoveByArea(areaId)` removes every table whose id starts with `<areaId>:`.
- `ItemRegistry.RemoveByArea(areaId)` removes every template whose id starts with `<areaId>:`.

The trailing colon is load-bearing: it is the id separator, so a prefix match cannot reach a
sibling area whose slug string-prefixes the target. `ItemRegistry`'s backing dictionary is
case-sensitive and its match is Ordinal; the other two are OrdinalIgnoreCase, matching their
comparers. All three are idempotent and return a count (or bool) rather than throwing on a miss.

`RuntimeNamespaceStore` deliberately has no removal path. A discarded area's pack survives the
discard, so its namespace legitimately still exists.

## Rejected and Reverted

- None on record.

## Change Log

- 2026-06-19 [registry-introspection](changes/2026-06-19-registry-introspection.md) - post-seal introspection readers (`GetRegistrySummary`/`GetRegistrations` + the two flat view records)
