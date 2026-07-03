---
release: 0.1.46
specs: [rest-and-recovery.md]
---

# Regen And Vitals Fixes

## Why

Three fixes that ride the same engine release, all surfaced while playtesting the
boss-combat slice-1 Myrddraal: regeneration kept healing fighters mid-fight, swell HP
changes never reached the web client, and the `_rate` naming holdout (`healing_rate`)
still lingered on the room regen bonus. The first two are pre-work for the larger
entity-state-mutation-broadcast seam; the third is the last slice of the vocabulary
consolidation, unblocked once the regen pre-work landed.

## What

- **Regen pauses in combat (tapestry#126).** The regen tick now skips any entity that is
  in combat (`CombatManager.IsInCombat`), so combat damage sticks for the duration of a
  fight - ROM-style "no heal while fighting." `RegisterRegenHandler` takes the
  `CombatManager` as a required parameter (interval and rest-config are required too); all
  call sites pass them explicitly.
- **Room regen bonus renamed `healing_rate` -> `rest_bonus` (vocabulary consolidation
  slice 6).** The room scope now reads the same `rest_bonus` property the furniture scope
  already used; the `healing_rate` / `RoomHealingRate` registration is deleted, retiring
  the last `_rate` key. `rest_bonus` is registered `transient`, so it is not admin-settable
  at the room scope (a deliberate consequence of the unification, not an oversight).
- **Swell HP pushed to the web client (tapestry-client#17), interim.** A swell resolve
  mutates HP directly without firing `combat.hit`, so `CharVitalsHandler` and
  `CharCombatHandler` now subscribe to `combat.swell.resolve` and refresh the affected
  player's own bar and target bar. This is a stopgap: the entity-state-mutation-broadcast
  seam will route swell writes through the general `entity.vital.changed` topic and revert
  these two subscriptions.
