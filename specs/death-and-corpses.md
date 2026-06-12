---
capability: death-and-corpses
last-updated: 2026-06-12
---

# Death and Corpses

## Overview

The death pipeline activates when any entity's HP reaches 0 in combat.
The canonical signal is the `entity.vital.depleted` event (vital = "hp").
Two subscribers handle the event in parallel: `CombatEventModule` (C#, wired
at server startup) drives combat teardown and publishes higher-level events;
pack scripts in `packs/@tapestry/core/scripts/` handle corpse creation,
loot transfer, gold award, and player recall. Corpse decay is managed by a
periodic tick handler registered in `TickHandlerModule`.

Out of scope here: combat round resolution that produces the depletion event
(see combat-resolution.md), mob aggro (see mob-ai.md).

---

## Behavior

### entity.vital.depleted Event

- Published by `ResolveAutoAttacksPhase` when `target.Stats.Hp` reaches 0
  during a swing. Vital field is `"hp"`. The swing loop breaks immediately
  after publication. (src/Tapestry.Engine/Combat/ResolveAutoAttacksPhase.cs:99)

- Event fields available to subscribers:
  - `SourceEntityId` -- Guid of the entity that died.
  - `data.vital` -- `"hp"`.
  - `data.attackerId` -- Guid string of the killer (present for ability
    one-shots; absent for auto-attack kills, in which case
    `CombatEventModule` falls back to the victim's primary target).
    (src/Tapestry.Server/Modules/CombatEventModule.cs:72)

- Two independent subscribers react to this event (execution order depends on
  registered priority and order within the JS engine):
  1. `CombatEventModule.WireDeathHandling` (C#, priority 100) -- drives
     combat teardown. (CombatEventModule.cs:62)
  2. `death.js` (NPC corpse handler) and `output.js` (player corpse handler)
     in the core pack -- drive corpse creation and loot transfer.
     (packs/@tapestry/core/scripts/mobs/death.js;
     packs/@tapestry/core/scripts/combat/output.js:131)

### CombatEventModule Death Handling

- Subscribed to `entity.vital.depleted` at priority 100.
  (src/Tapestry.Server/Modules/CombatEventModule.cs:62)

- Killer resolution: if `data.attackerId` parses to a valid Guid, that entity
  is the killer. Otherwise the victim's primary combat target is used as the
  fallback killer. (CombatEventModule.cs:72)

- A cancellable `entity.death.check` event is published before proceeding.
  Subscribers (e.g., a "cockroach" resurrect skill) can set
  `data.cancel = true` to intercept the death and prevent the default
  pipeline. (CombatEventModule.cs:82)

- If not cancelled, `CombatManager.HandleEntityDeath(victimId, killerId)` is
  called. (CombatEventModule.cs:101)

### CombatManager.HandleEntityDeath

- If a killer is known: triggers an alignment shift hook via `AlignmentManager`
  (if present), then publishes `combat.kill` with `killerName` and `victimName`
  in event data. (src/Tapestry.Engine/Combat/CombatManager.cs:278)

- If the victim is an NPC: publishes `mob.killed` with `templateId` and
  `mobName`. (CombatManager.cs:307)

- Calls `RemoveEntityFromAllCombat(victimId)`, which clears the victim from
  every opponent's combat list and publishes `combat.end` for each newly idle
  opponent and then for the victim. (CombatManager.cs:325)

### NPC Death Pipeline (death.js)

Triggered by `entity.vital.depleted` in the core pack script. Applies only to
entities with `type === "npc"`. Entities tagged `no_kill` are skipped as a
safety guard. (packs/@tapestry/core/scripts/mobs/death.js:2)

1. **Corpse entity creation.** A new entity of type `"container"` is created
   and named `"the corpse of <mobName>"`. The following tags are added:
   `corpse`, `container`, `no_get`. Properties set on the corpse:
   - `corpse_decay` -- taken from `entity.properties.corpse_decay`; defaults
     to 300 ticks if absent.
   - `corpse_created_tick` -- current game tick at time of death.
   - `template_id` -- the mob's template ID (may be null for dynamically
     spawned mobs with no template).
   - `mob_level` -- from `entity.properties.mob_level`; defaults to 1.
   (death.js:20)

2. **Loot transfer.** `tapestry.inventory.transferAll(mobId, corpseId)` moves
   all inventory items. `tapestry.equipment.transferAll(mobId, corpseId)` moves
   all equipped items. Both run before the mob entity is removed from the world.
   (death.js:30)

3. **Corpse placement.** The corpse entity is placed in the mob's last room.
   (death.js:34)

4. **Mob removal.** The mob entity is removed from the world via
   `tapestry.world.removeEntity`. (death.js:37)

5. **Room notification.** The room receives `"<mobName> has died!\r\n"`.
   (death.js:40)

6. **Gold award.** If the event carries a `killerId` and the mob has a
   `gold_min` property, a random gold amount in `[gold_min, gold_max]` is
   awarded to the killer via `tapestry.currency.addGold`. If `gold_max` is
   absent, `gold_min` is used as both bounds. Zero-gold results are silently
   skipped. (death.js:43)

7. **mob.death event.** Published with `templateId`, `mobName`, `roomId`,
   `corpseId`, and `killerId`. This event is the hook point for per-template
   `onDeath` callbacks dispatched by `ondeath-dispatch.js`.
   (death.js:59; packs/@tapestry/core/scripts/mobs/ondeath-dispatch.js)

### Player Death Pipeline (output.js)

Triggered by `entity.vital.depleted` in the core pack script. Applies only to
entities with `type === "player"`.
(packs/@tapestry/core/scripts/combat/output.js:131)

1. **Player corpse creation.** A container entity named
   `"the corpse of <playerName>"` is created with tags: `corpse`, `container`,
   `player_corpse`, `no_get`. Properties set:
   - `owner` -- entity ID of the player.
   - `corpse_decay` -- fixed at 600 ticks.
   - `corpse_created_tick` -- current game tick.
   (output.js:146)

2. **Gear unequip.** `tapestry.equipment.unequipAllSilent(entityId)` removes
   all equipped items silently (removes stat modifiers, moves items to the
   player's inventory without output). (output.js:156)

3. **Inventory transfer.** `tapestry.inventory.transferAllSilent(entityId, corpseId)`
   moves all inventory items to the corpse silently. (output.js:159)

4. **Corpse placement.** The corpse is placed in the room where the player died.
   (output.js:162)

5. **Room notification.** The room receives
   `"<death><playerName> has been slain!</death>\r\n"`. (output.js:165)

6. **Vital restoration.** `tapestry.stats.restoreVitals(entityId)` restores the
   player's HP (and other vitals) to full before teleporting. (output.js:168)

7. **Recall teleport.** Player is teleported to `entity.properties.recall` or
   `"tapestry-core:recall"` if that property is absent. (output.js:169)

8. **Player notification.** Three messages are sent to the player: a death
   announcement, the room name where the corpse was left, and a flavour
   message. Room description is then sent. (output.js:173)

9. **player.death event.** Published with `entityId`, `corpseId`, and `roomId`
   for pack extensions. (output.js:180)

### Corpse Decay Tick Handler

Registered by `TickHandlerModule.RegisterCorpseDecay` as a tick handler named
`"corpse-decay"` running every 30 ticks.
(src/Tapestry.Server/Modules/TickHandlerModule.cs:108)

Each iteration scans all entities tagged `"corpse"` and checks:

```
(currentTick - corpse_created_tick) >= corpse_decay
```

When the condition is met:

1. **Item dump.** If the corpse is in a room, each item in `corpse.Contents`
   is removed from the corpse and placed directly in the room. Items are not
   destroyed. (TickHandlerModule.cs:129)

2. **corpse.decayed event.** Published with `corpseName`, `wasPlayerCorpse`
   (boolean from the `player_corpse` tag), `roomId`, and `itemIds` (list of
   item ID strings that were dumped). (TickHandlerModule.cs:136)

3. **Corpse removal.** The corpse entity is removed from the room and then
   untracked from the world via `_world.UntrackEntityDeep(corpse)`. The room
   receives `"<corpseName> crumbles to dust.\r\n"`. (TickHandlerModule.cs:150)

Decay tick interval (30 ticks) is the polling cadence; the actual lifetime of
each corpse is controlled by its individual `corpse_decay` property (NPC
default 300, player fixed 600).

---

## Rejected and Reverted

- None on record.

---

## Change Log
