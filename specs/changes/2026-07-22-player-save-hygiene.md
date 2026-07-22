---
release: 0.1.50
specs: [flows-and-wizards.md, persistence.md]
---

# Player Save Hygiene

## Why

Wizard flows had only one place to keep working memory between steps - the entity
property bag via `entity.setProperty`. That bag is what the player serializer writes
to `player.yaml`, so every wizard answer leaked into the save and stayed there
forever (the link wizard alone left 14 `link_*` keys per run), and scalar answers
lost their real type on the round-trip. The serializer compounded it: an unregistered
key was not rejected but enveloped in a `{type, value}` tagged dict, so `player.yaml`
accumulated scratch nobody registered. Two fixes that belong together: give flows a
private store that never touches the save, and make the serializer drop unknown keys
by default so the save holds only registered values as a structural guarantee.

## What

- **Flow-scoped scratch store** (flows-and-wizards.md). Each `FlowInstance` owns an
  `IFlowScratch` (a `Dictionary<string, object?>` behind the interface) that lives
  exactly as long as the wizard and is never serialized - there is no code path from
  scratch to `PlayerSerializer`. The JS entity proxy gains a `scratch` handle:
  `scratch.get(key)` (real type preserved, `undefined` if absent), `scratch.set(key,
  value)`, `scratch.has(key)`. No `clear`/`remove` - the instance's death is the clear.
  Step scripts use it for working memory; `entity.setProperty` stays for real
  registered character fields (`class`, `race`).
  (src/Tapestry.Engine/Flow/IFlowScratch.cs;
  src/Tapestry.Scripting/Modules/FlowsModule.cs)

- **Scratch threaded to every callback** (flows-and-wizards.md). All step delegates
  (`skip_if`, `recommend_field`, `text`, `prompt`, `options`, `on_select`, `on_input`,
  `on_yes`, `on_no`) and `on_complete` widen from `(entity, ...)` to `(entity, scratch,
  ...)`; `validate` is the sole exception (it receives only the raw input string). The
  `FlowInstance` passes its own scratch at each invocation.
  (src/Tapestry.Engine/Flow/FlowStepDefinition.cs;
  src/Tapestry.Engine/Flow/FlowDefinition.cs;
  src/Tapestry.Engine/Flow/FlowInstance.cs)

- **flows.trigger seed + recommend-context from scratch** (flows-and-wizards.md).
  `tapestry.flows.trigger(entityId, triggerName[, seed])` copies an optional plain
  object into the new instance's scratch at creation, for the one working-memory key a
  command sets before the flow exists (the `edit area` command seeds `{ edit_area:
  areaId }`). The area recommend-context reads `edit_area` from scratch instead of the
  entity property. Jint marshals an omitted delegate arg as CLR null, so the seed guard
  null-checks before reading `.Type` - two-arg callers do not throw.
  (src/Tapestry.Scripting/Modules/FlowsModule.cs;
  src/Tapestry.Engine/Flow/FlowEngine.cs)

- **Serializer drops unknown keys** (persistence.md). `PlayerSerializer.SerializeProperties`
  writes only registered non-transient keys (type-preserving). An unregistered key is
  dropped and logs one structured warning naming the key and the owning entity (type +
  id + name); the old `{type, value}` envelope is no longer written. The load side is
  unchanged - it still reads any legacy envelope on an old save and self-heals it to
  bare on the next save, so existing saves clean themselves with no migration step.
  `SerializeTaggedValue` is removed (its sole caller, the unknown-key path, is gone).
  (src/Tapestry.Engine/Persistence/PlayerSerializer.cs:171-207)

- **distributed_from registered as a keeper** (persistence.md). `distributed_from` is
  engine-set on distributed item instances and read back for replenish top-up counting;
  it rode the old unknown-key envelope, so the flip would have dropped it. Registered as
  a non-transient `String` item property (`ItemProperties.DistributedFrom`) before the
  flip so it survives; `DistributionService` references the shared const. A prod-corpus
  census confirmed it was the only keeper - no other unregistered key needed rescuing.
  (src/Tapestry.Engine/Items/ItemProperties.cs;
  src/Tapestry.Engine/Distribution/DistributionService.cs)

- **Sidecar principle for resumable flows** (persistence.md). No current flow survives a
  disconnect, so nothing resumable is built. The decided pattern is recorded so a future
  feature does not reach for `player.yaml` rows: a resumable flow gets a dedicated
  sidecar file (e.g. `flows.yaml`) in the player folder, written while suspended and
  deleted on completion or abandonment - never rows in the registry-gated character save.
