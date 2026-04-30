# Spec Review: Phase 7 — Entity Model + Persistence

- **Spec:** `docs/superpowers/specs/2026-04-13-entity-persistence-design.md`
- **Reviewer:** Travis Haley
- **Date:** 2026-04-13 00:54 MT
- **Branch:** master
- **Verdict:** Changes Requested

## Summary

Well-structured spec covering the full lifecycle of player persistence — stable IDs, serialization, save triggers, authentication, and shutdown. The core design (PropertyTypeRegistry, flat item list with ID references, tolerant reader versioning) is sound and fits the existing architecture cleanly. A few issues need resolution before implementation: the dynamic property key gap in the type registry, a missing SessionManager capability for reconnection, and underspecified concurrent save safety on the game loop thread.

## Findings

### Problem-solution fit
- **Severity:** Praise
- The spec stays focused: players persist, everything else respawns from templates. Goals and non-goals are crisp. The "save data vs template data" boundary is explicitly drawn. Content packs stay untouched. Right scope for Phase 7.

### Scope sanity
- **Severity:** Suggestion
- Authentication (passwords, lockout, session takeover, seed admin, password reset) is significant scope layered on top of persistence. It's well-designed and clearly needed — can't have persistence without auth gating — but roughly doubles the implementation surface. Consider whether password reset (Section 6.4) could be deferred to a fast-follow. It's not needed for the core persistence loop and could ship separately without blocking anything.

### Clarity & completeness — Dynamic property keys in PropertyTypeRegistry
- **Severity:** Concern
- The spec shows static registration for fixed keys (`regen_hp`, `template_id`, etc.), but `ProgressionProperties` and `CombatProperties` use dynamic key patterns: `level:{trackName}`, `xp:{trackName}`, `ac_{damageType}`. The registration example only shows fixed keys. How does `PropertyTypeRegistry.GetType("level:combat")` resolve?
- Options that should be called out:
  - **Prefix matching**: `RegisterPrefix("level:", typeof(int))` — registry checks prefixes when exact match fails
  - **Wildcard registration**: register the pattern, deserializer matches via `StartsWith`
  - **Caller registers at runtime**: `ProgressionManager` registers `level:combat` when a track is first used — but a cold load before the first XP gain would miss them
- Without this, all progression and per-damage-type AC data falls through to the tagged fallback format. Functional, but produces ugly save files for core engine data.

### Clarity & completeness — SessionManager has no name-based lookup
- **Severity:** Concern
- The reconnect flow (Section 6.2) requires finding an existing session by player name ("Already connected? kick old session"). Currently `SessionManager` only indexes by connection ID and entity ID — no name-based lookup. The "Files Changed" table doesn't mention `SessionManager`. The spec should note whether `SessionManager` gets a name-based index or the reconnect check walks `AllSessions`.

### Architecture soundness — Save ordering on disconnect
- **Severity:** Concern
- `GameLoopService.OnDisconnect` currently removes the entity from the room, untracks it from `World`, then removes the session. The spec says "save current state before session teardown." If the save reads `entity.LocationRoomId` after `Room.RemoveEntity` clears it to `null`, the saved location is lost.
- The spec should explicitly state that save runs **before** the current disconnect handler, or that `PlayerPersistenceService` captures location before teardown.

### Architecture soundness — Async saves on the game loop thread
- **Severity:** Suggestion
- Event-driven saves are described as "fire-and-forget — save runs asynchronously, does not block the game tick." The game loop is single-threaded. If `SaveAsync` writes to disk on a background thread while the game loop mutates the entity on the next tick, the entity's `_properties` dictionary is read concurrently (not thread-safe).
- Recommended: serialize the `PlayerSaveData` DTO synchronously on the game loop thread (snapshot), then write to disk async. Overhead of building the DTO is negligible.

### Architecture soundness — Flat item list with ID references
- **Severity:** Praise
- The container/equipment/inventory model using a flat list with ID references is exactly right. Avoids recursive YAML nesting, maps to relational storage later, handles the multi-death corpse scenario cleanly.

### Architecture soundness — StatBlock serialization
- **Severity:** Praise
- Saving base + vitals + modifiers, relying on `_dirty = true` default for cache rebuild. No changes to StatBlock needed. Correctly calls out NOT saving cached/effective values.

### Phasing & deliverability
- **Severity:** Praise
- Player load-on-login rather than load-all-on-startup is the right call. Startup sequence change in Section 8 is clearly ordered. Each section is independently testable.

### Risk & failure modes — Atomic write on Windows
- **Severity:** Suggestion
- Section 5.2 specifies atomic writes via "write to `.tmp` then rename." On Windows/NTFS, `File.Move(src, dst, overwrite: true)` does a delete+rename, not an atomic swap. If the process crashes between delete and rename, the save file is gone.
- Risk is low (autosave every 5 min, window is microseconds), but the spec should acknowledge platform behavior. Safer pattern: write `.tmp`, rename old to `.bak`, rename `.tmp` to target, delete `.bak`.

### Risk & failure modes — No player save command
- **Severity:** Concern
- No explicit player-initiated `save` command. Save triggers are: creation, disconnect, autosave, death, level-up, password change. If a player grinds items for 4 minutes then hard-crashes, autosave may not have fired. MUD players expect to type `save`. One-line command registration in the core pack — should be in scope.

### Testing strategy
- **Severity:** Suggestion
- Section 11 says all 232+ tests must pass but doesn't describe **new** tests. Recommended categories:
  - `PropertyTypeRegistry` unit tests (known key, unknown key, bad value, default behavior)
  - `PlayerSerializer` round-trip tests (entity -> DTO -> entity, all fields survive)
  - `FilePlayerStore` integration tests (write/read/delete, atomic write, directory creation)
  - Login flow telnet scenarios (new player, returning player, wrong password, lockout, reconnect)
  - Autosave tick handler test
  - Graceful shutdown save test

### Clarity & completeness — Project ownership of new files
- **Severity:** Suggestion
- Section 13 lists new files but doesn't specify which project they live in. `PropertyTypeRegistry` is a persistence concern but used by Engine's `*Properties.cs` — does it go in Engine or Data? `IPlayerStore` and `FilePlayerStore` — Engine or Data? `PlayerPersistenceService` — Engine or Server? The spec should be explicit.

## Prior Reviews

First review.
