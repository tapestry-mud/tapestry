---
release: 0.1.45
specs: [combat-resolution.md, death-and-corpses.md, economy-and-shops.md, items-and-containers.md, mob-ai.md, mob-lifecycle.md, persistence.md, world-entity-store.md, world-simulation.md]
---

# Vocabulary Consolidation

## Why

The engine had accumulated tag/property vocabulary drift: one concept carried two
names (flee vs wimpy, shop.sells vs shop_sells), one name did two jobs (fill_source was
both a tag and a property, terrain was both an exposure axis and a biome), and several
engine-read tags were declared nowhere. Drift like that is a standing source of silent
bugs - a rename that misses one reader, a typo in a HasTag string that never resolves,
a property registered in a pack that the engine also registers. This pass settles one
grammar and enforces four rules: a name is never both a tag and a property; the same
concept keeps one name across scopes; whoever reads a name declares it in the same
change; no dotted keys.

## What

- **Flee unified (combat-resolution, mob-ai).** `flee_threshold` and `wimpy_threshold`
  collapse to one property `wimpy_pct` (int 0-100). A single predicate
  `CombatManager.ShouldFlee(entity, currentTick)` is the sole flee decision; both trigger
  points (the wimpy combat phase and the mob-AI flee path) call it. The old
  `FleeThreshold` registration is deleted.
- **Mob level desugars (mob-lifecycle, death-and-corpses).** A template's `mob_level`
  authoring key desugars to a `level: {combat: N}` map at `MobTemplate.CreateEntity` (the
  class-independent path), so a live mob no longer carries a scalar `mob_level`. The key
  survives as an authoring key and as explicit corpse metadata (a corpse is a container
  outside the mob appliesTo, so death stamps `mob_level` from `level.combat`).
- **Economy (economy-and-shops).** `value` moves to an engine-registered property; the
  dotted `shop.sells`/`shop.buy_markup`/`shop.sell_discount` keys and the three-spelling
  loader shim are retired in favor of the flat `shop_sells` list plus
  `shop_buy_modifier`/`shop_sell_modifier`. The global world-default markup/discount config
  is a separate namespace and is untouched.
- **Inventory (items-and-containers).** Fill liquid is read from the `fill_type` property
  (the water hardcode is gone); the `fill_source` property registration is deleted while
  `fill_source` survives as a source-marker tag. `fixture` now implies `no_get` at the
  get-message reader and the get-all filter, matching the pickup gate.
- **World (world-simulation, world-entity-store).** `terrain` is a closed set
  (indoors/outdoors/underground) defaulting to `outdoors` everywhere, including the GMCP
  room environment field; biome carries its own axis. Weather and time-of-day message
  lookup resolves biome-first, then falls back to terrain. Engine-read tags now have a
  single declaration site so a mistyped tag fails resolution instead of silently never
  matching.
- **Persistence.** The player serializer gains a read-old-write-new key map: a save
  carrying `wimpy_threshold` or the ROM negation trio (`notell`/`nochannels`/`noemote`)
  loads as `wimpy_pct` / `no_tell`/`no_channels`/`no_emote` and writes back the new key,
  so no player setting resets on upgrade.

The `_rate` suffix family is retired (its one remaining member, room regen bonus, ships
in a follow-up). `rare_bonus` was reviewed and kept as-is.
