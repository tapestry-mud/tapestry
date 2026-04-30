# Plan Review: Training and Practicing (Phase 12.75)

- **Plan:** `docs/superpowers/plans/2026-04-20-training-and-practicing.md`
- **Spec:** `docs/superpowers/specs/2026-04-20-training-and-practicing-design.md`
- **Reviewer:** Travis Haley
- **Date:** 2026-04-20 20:11 MT
- **Branch:** master
- **Verdict:** Approved

## Summary

Plan is ready for execution. All five findings from the first review pass have been resolved cleanly. Structure remains strong — 14 commit-sized tasks (Task 11.5 added for scenario scaffolding), TDD throughout, explicit JS API verification steps before command authoring, and a real `TrainingConfig.SetTrainable` runtime toggle backing the `settrainable` admin command.

## Findings

### 1. Problem-solution fit
- **Severity:** Praise
- Tasks trace directly to spec Section 6 rollout order. "Spec Clarifications" table at the top still proactively catches where the spec's API names diverged from actual code (`getDefinition` vs `get`, `TrainerConfig` post-processing in `PackLoader`, `StatBlock.Invalidate` location, extended `StatGrowthOnLevelUp.Subscribe` signature).

### 2. Scope sanity
- **Severity:** Looks good
- 14 tasks, proportional to the spec's three linked systems. No task is oversized. Task 11.5 is a focused scaffolding task that unblocks all 10 telnet scenarios.

### 3. Clarity & completeness
- **Severity:** Resolved (was Concern)
- `settrainable` is now a genuine implementation. `TrainingConfig.SetTrainable(stat, enabled)` lives in the config object (plan line 585), is exposed via `TrainingModule.setTrainable` (line 1693), and backs the admin command (line 2434). Single mutation point, clean layering.

### 4. Assumptions & dependencies
- **Severity:** Resolved (was Concern — biggest blocker)
- Task 11.5 adds `setprof` and `setstat` admin commands, which are the scaffolding every telnet scenario depends on. `setprof` routes through the existing `tapestry.abilities.setProficiency`; `setstat` uses `tapestry.stats.setBase` (added in Task 8). Every scenario that previously depended on a non-existent admin command (`setprof`, `setcon`, etc.) is now supported. `setseed` and `forceabilities` are not added — the failure-gain scenario acceptance criterion is probabilistic by design (documented in the scenario file).

### 5. Architecture soundness
- **Severity:** Resolved (was minor suggestion)
- `StatGrowthOnLevelUp.Subscribe` now takes a required `TrainingManager trainingManager` parameter (line 1431) — no more optional-null default. Tests pass it explicitly; the DI-driven production path can't silently skip Trains granting.

### 6. Phasing & deliverability
- **Severity:** Looks good
- Each task is independently committable with its own tests. Migration safety is preserved (`GetCap` defaults to 100 when the cap property is absent, so pre-migration players keep current behavior). Task 11.5 ordering is correct — it runs after Task 11's main admin commands land and before Task 13 scenarios.

### 7. Risk & failure modes
- **Severity:** Resolved (was Concern)
- Task 8 now includes explicit `grep -n` verification steps against `StatsModule.cs` and `RacesModule.cs` before writing `train.js`, with two conditional fallback paths (use `tapestry.stats.get` if present, else read individual stats via `tapestry.world.getProperty`). `tapestry.stats.setBase` and `tapestry.races.getStatCap` are added where missing as part of Task 8. This closes the previous runtime-failure risk cleanly.
- Passive-ability failure-gain: Task 5 Step 6 continues to resolve this as "no changes needed" (passives fire on success only). Confirmed intentional.

### 8. Testing strategy
- **Severity:** Looks good
- Strong TDD discipline: failing test → run → implement → pass → commit on every task. Coverage includes unit tests (Engine + Scripting), JS module round-trip tests, and 10 telnet integration scenarios. The `failure-gain.md` scenario's probabilistic assertion is documented and appropriate.

## Prior Reviews

First review — conducted inline during the brainstorm→plan transition on 2026-04-20, findings addressed before this document was committed.
