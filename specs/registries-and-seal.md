---
capability: registries-and-seal
last-updated: 2026-06-12
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
  Commit). The seal barrier calls `RegistrationPolicy.Resolve()` once at the end of boot;
  it groups candidates by `(Kind, Name)`, picks order-independent winners, and replays each
  winner's Commit.
  (src/Tapestry.Engine/Registration/RegistrationPolicy.cs:8-15,84-101)

- **Collision policy:** A same-name registration from two packs is a strict boot error
  unless exactly one pack declares `{ override: true }` with a valid dependency edge on the
  owner pack. `kernel` and `engine` owners are non-overridable.
  (src/Tapestry.Engine/Registration/RegistrationPolicy.cs:32-33,122-180)

- **Post-seal registration (D5):** The policy supports sanctioned runtime registrations
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

## Rejected and Reverted

- None on record.

## Change Log
