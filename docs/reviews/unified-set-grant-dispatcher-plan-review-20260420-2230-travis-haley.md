# Plan Review: Unified `set` + `grant` Admin Dispatcher (Phase 12.8)

- **Plan:** `docs/superpowers/plans/2026-04-20-unified-set-grant-dispatcher.md`
- **Spec:** `docs/superpowers/specs/2026-04-20-unified-set-grant-dispatcher-design.md`
- **Reviewer:** Travis Haley
- **Date:** 2026-04-20 22:30 MT
- **Branch:** master
- **Verdict:** Approved

## Summary

Plan is ready for execution. Both material findings from the first review pass are resolved. Structure is strong — 16 commit-sized tasks mapping cleanly to spec Section 10 rollout order, TDD discipline throughout, and an unusually rigorous Spec Clarifications table that catches five real divergences between spec language and actual code (`MaxHp` read-only, `SessionManager` injection point, `ApiWorld` field naming, `tapestry.world.setProperty` location, `PanelRenderer` typed-object path).

## Findings

### 1. Problem-solution fit
- **Severity:** Praise
- Tasks trace directly to spec Section 10 rollout order (1→16). The Spec Clarifications table at the top is thorough and accurate — every claim I verified held up against the code: `MaxHp` is indeed computed via `Recalculate()` (`StatBlock.cs:114`) with `BaseMaxHp` + `Invalidate` as the correct write path; `SessionManager.AllSessions` exposes the online-player iterator needed for player resolution (`PlayerSession.cs:128`); `tapestry.world.setProperty` is confirmed at `WorldModule.cs:88`; `resolvePlayer` in `admin.js` remains in use by `teleport` after the legacy-command deletions. Without these catches, several Tasks would have hit runtime errors.

### 2. Admin-tag guard in command shims
- **Severity:** Resolved (was Concern — blocker)
- First-pass finding: Task 8's `set` and `grant` command shim handlers called `tapestry.admin.set.dispatch(...)` without guarding on `player.hasTag('admin')`. `CommandRouter.Route` (`src/Tapestry.Engine/CommandRouter.cs:16-32`) does not gate handler invocation on `VisibleTo` — the predicate is display-only (used by `CommandsModule.listForPlayer` to hide admin commands from non-admins in help output). Task 8's own `admin-set-unknown.md` scenario ("non-admin gets Huh?") would have failed as written.
- Revision adds the explicit guard as the first line of both shims (plan lines 1567, 1577):
  ```javascript
  if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
  ```
  This matches the convention every other admin command in `admin.js` already follows. Scenario path is now correct end-to-end.

### 3. Scenario audit scope
- **Severity:** Resolved (was Concern)
- First-pass finding: Task 14 named two files (`admin-setcap.md`, `admin-granttrains.md`) as the migration targets. Actual grep across `packs/legends-forgotten/tests/` returned 14 files using the deleted commands — 12 beyond the two named. The original audit Step was correct in principle but the "Known files: 2" framing anchored scope low, and the 12.75 test suite (`practice-*`, `train-*`, `failure-gain`, `stat-affected-growth`) all depend on the scaffolding commands being migrated.
- Revision enumerates all 14 files in the File Map block at the top of Task 14 and in the final commit command. Steps 1-2 handle the two originally-named files with full before/after content. Steps 3-5 group the remaining 12 files by logical cluster (skill-tree, class/quest, training) and direct the implementor to apply the spec Section 7.2 migration table per file. Scenario semantics preserved — work is mechanical.

### 4. Mob template references in Task 10
- **Severity:** Resolved (was Minor)
- First-pass finding: `admin-set-npc-hp.md` scenario used `core:test-dummy`; `admin-set-ordinal.md` used `core:trolloc`. Template IDs needed verification.
- Revision adds an explicit note at the top of Task 10 confirming `core:test-dummy` exists in `packs/tapestry-core/entities/mobs.yaml`, and flagging that `core:trolloc` does not exist — only `lf:trolloc_warrior` (legends-forgotten pack). Ordinal scenario updated to use `lf:trolloc_warrior`. No surprise at implementation time.

### 5. Test-room setup in Task 4
- **Severity:** Acknowledged (was Minor)
- First-pass finding: Task 4's NPC/ordinal unit tests assume `world.GetRoom("test:spawn")` with a fallback path ("If room creation is complex, skip the NPC tests in Task 4 and rely on telnet scenarios for NPC resolution coverage").
- Revision left this as-is. The fallback path is documented, and telnet scenarios in Task 10 (`admin-set-npc-hp.md`, `admin-set-ordinal.md`) provide end-to-end coverage of the resolver regardless. Accepted — branching at implementation time is acceptable for a non-load-bearing unit-test harness decision.

### 6. Clarity & completeness
- **Severity:** Looks good
- Self-review matrix at plan lines 2667-2690 maps every spec section to the task that implements it. Handler contract (spec §1.3) is covered by Task 5's `BuildAdminObj` method; pack attribution (spec §1.4) is covered by Task 2's `__currentPack` capture; subtype gating (spec §3) covered by Task 5's `ReadSubtype` + `applies_to` check with unit test `Dispatch_SubtypeMismatch_DoesNotInvokeHandler`.

### 7. Scope sanity
- **Severity:** Looks good
- 16 tasks, each independently committable with its own tests. Task 8 (shims) before Task 9 (register types) deliberately creates a window where `set` exists but has no registrations; the plan handles this with `admin-set-unknown.md` in Task 8, which exercises the unknown-kind/unknown-type paths specifically. Task 13's reference-doc rewrite (`rom_admin_commands.md`) is a focused scope-matching update — retracts the superseded "type-first" framing, adopts kind-first.

### 8. Assumptions & dependencies
- **Severity:** Looks good
- All API assumptions verified in the Spec Clarifications table. Tasks 1-7 build the module in dependency order (skeleton → register → listTypes → resolveTarget → dispatch → panels → setEntityHp). No task forward-references something a later task hasn't yet produced.

### 9. Architecture soundness
- **Severity:** Looks good
- `PanelRenderer` injected directly is the right call — avoiding Jint round-trip for internal panel construction keeps the help path out of the scripting engine entirely. `setEntityHp` as a dedicated bridge method (rather than trying to shoehorn HP updates through `tapestry.stats.setBase`) correctly isolates the `StatBlock.BaseMaxHp + Invalidate + clamp` sequence where it belongs. Pack attribution via `__currentPack` follows the established `CommandsModule` pattern.

### 10. Testing strategy
- **Severity:** Looks good
- Strong TDD discipline: failing test → run → implement → pass → commit on every task. Coverage includes unit tests (AdminModule: register, listTypes, resolveTarget, dispatch, subtype gating, setEntityHp), JS round-trip tests (Task 15), and 14 telnet scenarios (new + migrated). `Dispatch_HappyPath` test uses `self` as target to avoid needing full session setup — clean separation from the session-dependent resolver tests in Task 4.

## Prior Reviews

First review — conducted inline during the plan-writing transition on 2026-04-20, findings addressed before this document was committed.
