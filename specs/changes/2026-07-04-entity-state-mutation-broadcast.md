---
release: 0.1.47
specs: [gmcp.md, events.md, combat-resolution.md, persistence.md, rest-and-recovery.md, abilities.md]
---

# Entity State Mutation Broadcast

## Why

The GMCP web client went stale whenever a vital or an observable status changed
through a path that had no hand-wired GMCP event. Four bugs were witnessed from
the same root shape - a stat or property write that skipped the one place the
client listens: a Myrddraal swell counter/whiff moved HP without firing
`combat.hit`, so neither the player bar nor the boss target bar refreshed (the
interim `combat.swell.resolve` subscription shipped in 0.1.46 was a stopgap for
exactly this); chip damage from some paths was not reflected; an admin restore did
not update the bar; and eating out of famished never pushed a `Char.Status`
update. Rather than keep hand-wiring a GMCP subscription per new write site, this
change routes every vital and observable-property mutation through a single
publishing seam, so the client cannot miss one.

## What

- **A publishing `VitalsService` is now the sole path for every hp/resource/movement
  write.** `Apply`, `Set`, and `RestoreToMax` clamp the write and publish one
  `entity.vital.changed` topic (`{ vital, old, new, delta, reason }`) only when the
  clamped value actually changes. Combat auto-attack (`combat.melee`), swell resolve
  (`combat.swell`), ability cost (`ability.cost`), pack `applyDamage` (`ability.damage`),
  flee, regen (`regen`), and the JS and admin stat surfaces all route through it.
  `StatBlock`'s hp/resource/movement setters are now private, so the compiler enforces
  the seam; the migration's completeness was proven by a clean warnings-as-errors build
  after the setters were sealed, not just asserted. init and load writes deliberately use
  a separate non-publishing baseline path.
- **GMCP vitals and target bars now subscribe to `entity.vital.changed` alone.**
  `CharVitalsHandler` marks the entity dirty on that one topic (the per-tick
  `Char.Vitals` batcher is unchanged); `CharCombatHandler` refreshes every viewer whose
  combat list contains the changed entity via the bidirectional
  `CombatManager.GetCombatList`. The three prior vitals subscriptions (`ability.used`,
  `entity.regen`, `entity.vital.depleted`) and the interim 0.1.46 `combat.swell.resolve`
  stopgap are dropped; `combat.engage` / `combat.end` / `combat.kill` stay for
  combat-list membership changes.
- **Observable property-bag writes now publish `entity.status.changed`.**
  `PropertyRegistry` gains an independent case-insensitive observable-topic map
  (`RegisterObservable` / `TryGetObservableTopic`), validated so an observable
  registration with no change topic throws. `EntityStatusBroadcaster`, an
  `IPropertyObserver` attached to every tracked entity by `World`, publishes
  `entity.<topic>.changed` (`{ key, old, new }`) when `Entity.SetProperty` actually
  changes an observable key. `sustenance` and `alignment` are declared observable on the
  `status` topic; `CharStatusHandler` subscribes and resends `Char.Status`, so eating or
  drinking out of famished updates `hungerValue` on the client with no manual re-look.
