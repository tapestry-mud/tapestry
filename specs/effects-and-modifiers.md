---
capability: effects-and-modifiers
last-updated: 2026-06-12
---

# Effects and Modifiers

## Overview

The effects system tracks discrete, timed (or permanent) bundles of stat
modifiers and entity flags in memory. Effects are created by successful ability
hits (via `EffectManager.TryApply`, called from `AbilityResolutionPhase`) or
directly by pack scripts via the `effects` JS API. `EffectManager` owns the
active-effect list per entity, handles tick-based expiry, and publishes events
consumed by `CharEffectsHandler`, which broadcasts the current effect list to
the player's GMCP connection.

Out of scope here: ability definitions and the resolution pipeline that calls
`TryApply` (abilities.md), auto-attack damage (combat-resolution.md).

---

## Behavior

### Effect Model

- `ActiveEffect` fields: `Id`, `SourceAbilityId` (nullable), `SourceEntityId`,
  `TargetEntityId`, `RemainingPulses` (mutable), `StatModifiers`, `Flags`,
  `Metadata`. (src/Tapestry.Engine/Effects/ActiveEffect.cs)

- `RemainingPulses < 0` signals a permanent effect (never expires).
  (src/Tapestry.Engine/Effects/EffectManager.cs:138)

- `StatModifier` source is set to `"effect:{effectId}"` at apply time so
  modifiers can be tracked and removed cleanly without an additional lookup.
  (src/Tapestry.Engine/Heartbeat/AbilityResolutionPhase.cs:424;
  src/Tapestry.Scripting/Modules/EffectsModule.cs:75)

### EffectManager

- `TryApply(effect)` rejects (returns false) if an effect with the same `Id`
  is already active on the target entity -- no stacking of identical IDs.
  (EffectManager.cs:32)

- On apply: each stat modifier is applied via `entity.Stats.AddModifier` with
  a duplicate guard (skipped if same Source+Stat already present, to survive
  save/load without double-application). Each flag is added as an entity tag.
  Publishes `effect.applied`. (EffectManager.cs:175)

- `Remove(entityId, effectId)` reverses modifiers via
  `Stats.RemoveModifiersBySource` and removes tags. Publishes `effect.removed`.
  (EffectManager.cs:56)

- `RemoveByFlag(entityId, flag)` removes all effects carrying the given flag,
  reversing each. Publishes `effect.removed` per removal. (EffectManager.cs:85)

- `TickPulse()` decrements `RemainingPulses` on all non-permanent effects; any
  that reach <= 0 are expired: modifiers/tags reversed, `effect.expired`
  published. Called by `ResolveStatusEffectsPhase` each combat pulse.
  (EffectManager.cs:130; combat-resolution.md)

### Pack JS API -- effects

- `effects.apply(targetIdStr, def)` -- `def` shape:
  `{id, duration (pulses; -1 = permanent), source_entity_id?, stat_modifiers?,
  flags?}`. Returns bool (false if target unknown or effect already active).
  (src/Tapestry.Scripting/Modules/EffectsModule.cs:32)

- `effects.remove(entityIdStr, effectId)` -- explicit removal.

- `effects.removeByFlag(entityIdStr, flag)` -- bulk removal.

- `effects.hasEffect(entityIdStr, effectId)` -- returns bool.

- `effects.getActive(entityIdStr)` -- returns array of
  `{id, name, remaining_pulses, flags}`. Name resolves via `AbilityRegistry`
  (SourceAbilityId), falls back to SourceAbilityId then id. (EffectsModule.cs:152)

### GMCP -- Char.Effects

- `CharEffectsHandler` subscribes to `effect.applied`, `effect.removed`, and
  `effect.expired`. On any of these events it calls `SendEffects(entityId)`,
  which looks up the entity and sends a full `Char.Effects` refresh via
  `_connectionManager.Send(entityId, ...)`. There is no player-type guard in
  `SendEffects`; non-player entities simply have no GMCP connection, so the
  send is a no-op for them.
  (src/Tapestry.Server/Gmcp/Handlers/CharEffectsHandler.cs:40-69)

- Payload shape: `{effects: [{id, name, remainingPulses, flags, type}]}`.
  `type` = `"debuff"` if flags contains `"harmful"`, otherwise `"buff"`.
  (CharEffectsHandler.cs:75)

- `SendBurst` (called on login/reconnect) pushes the current full list to the
  new connection immediately. (CharEffectsHandler.cs:59)

---

## Rejected and Reverted

- None on record.

---

## Change Log
