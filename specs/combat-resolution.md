---
capability: combat-resolution
last-updated: 2026-06-12
---

# Combat Resolution

## Overview

The combat system is a turn-based, round-driven system implemented in
`src/Tapestry.Engine/Combat/`. All state lives in `CombatManager`; no game
content is hardcoded there. Pack scripts supply player-facing commands and
event-driven output. Rounds fire on a heartbeat cadence of 20 ticks (~2 s)
via `CombatPulse`. Mob aggro and GMCP updates are wired in `Tapestry.Server`.

Out of scope here: combat pulse timing (see heartbeat.md), mob aggro
disposition logic (see mob-ai.md), death pipeline (see death-and-corpses.md).

---

## Behavior

### Initiation

- `CombatManager.Engage(attacker, target, tick)` is the single entry point for
  starting combat. (src/Tapestry.Engine/Combat/CombatManager.cs:57)

- Engage returns `false` and takes no action if any of the following hold:
  - Target carries the `no_kill` tag. (CombatManager.cs:59;
    CombatManagerTests.cs:Engage_RejectsNonKillableTarget)
  - Attacker's current room carries the `safe` tag. (CombatManager.cs:64;
    CombatManagerTests.cs:Engage_RejectsInSafeRoom)
  - Attacker is within the flee cooldown window. (CombatManager.cs:73;
    CombatManagerTests.cs:Engage_RejectsDuringFleeCooldown)
  - Attacker already has the target in its combat list. (CombatManager.cs:78)

- On success, `combat.engage` is published to the event bus with attacker and
  target names and room ID. (CombatManager.cs:86)

- Entities are added to each other's combat lists symmetrically: the attacker
  adds the target and vice versa. (CombatManager.cs:83)

- The `kill` and `attack` commands (aliased) call `combat.engage` via the JS
  API. Players who are resting or sleeping cannot use the command.
  (packs/@tapestry/core/scripts/combat/commands.js:1)

- `mob.aggro` events are handled in `CombatEventModule`, which calls
  `CombatManager.Engage` directly to start aggro combat.
  (src/Tapestry.Server/Modules/CombatEventModule.cs:43)

### Combat List Model

- Each entity holds a `List<Guid>` of current opponents keyed by its own
  `Guid`. (CombatManager.cs:22)

- The first element of an entity's combat list is its primary target.
  (CombatManager.cs:108)

- When the first attacker engages a mob, the mob's primary target is set to
  that attacker. A second attacker engaging the same mob is appended to the
  mob's list but does not change its primary target.
  (CombatManagerTests.cs:Engage_SecondAttacker_MobKeepsPrimaryTarget)

- `SetPrimaryTarget` moves any existing opponent to the front of the list
  (used by taunt, rescue, and threat mechanics).
  (CombatManager.cs:121)

- `RemoveFromCombat(A, B)` removes each from the other's list and publishes
  `combat.end` for any side whose list is now empty.
  (CombatManager.cs:141)

- `RemoveEntityFromAllCombat(id)` clears the entity from every opponent list
  and publishes `combat.end` for each newly idle opponent, then for the entity
  itself. (CombatManager.cs:156;
  CombatManagerTests.cs:RemoveEntityFromAllCombat_CleansUpEverything)

- When a primary target is removed, the next entity in the list automatically
  becomes primary -- no explicit retarget needed.
  (CombatManagerTests.cs:Retarget_OnPrimaryTargetRemoved_ShiftsToNext)

### Phase System and Combat Pulse

- `CombatPulse` fires every 20 ticks. Priority 100 among pulse handlers.
  (src/Tapestry.Engine/Heartbeat/CombatPulse.cs:12)

- Within each pulse, `AbilityResolutionPhase` runs first, then all
  `ICombatPhase` implementations ordered by their `Priority` integer
  (ascending). (CombatPulse.cs:22;
  CombatPulseTests.cs:Execute_RunsAbilityPhaseFirst_ThenCombatPhasesInPriorityOrder)

- Default phases and priorities:
  - `ResolveAutoAttacksPhase` -- priority 200
    (src/Tapestry.Engine/Combat/ResolveAutoAttacksPhase.cs:9)
  - `ResolveStatusEffectsPhase` -- priority 300
    (src/Tapestry.Engine/Combat/ResolveStatusEffectsPhase.cs:7)
  - `CheckWimpyPhase` -- priority 400
    (src/Tapestry.Engine/Combat/CheckWimpyPhase.cs:7)

- Phases are sorted at construction time and re-sorted per pulse execution.
  (CombatManager.cs:44; CombatPulse.cs:29)

- Phases implement `ICombatPhase { string Name; int Priority; void Execute(PulseContext); }`.
  (src/Tapestry.Engine/Combat/ICombatPhase.cs)

### Auto-Attack Resolution

- `ResolveAutoAttacksPhase` iterates all entities with a non-empty combat list,
  players before NPCs. (ResolveAutoAttacksPhase.cs:14)

- Each combatant swings at its primary target only. Swing count is 1 plus any
  extra attacks granted by passive abilities.
  (ResolveAutoAttacksPhase.cs:40)

- If attacker and target are in different rooms, the engagement is removed
  before the swing. (ResolveAutoAttacksPhase.cs:33)

- Before each swing, defensive passives on the defender are checked. A
  successful defensive passive triggers `combat.evade` and skips that swing
  entirely. (ResolveAutoAttacksPhase.cs:51)

- Hit resolution delegates to `HitResolver.ResolveHit`.
  (ResolveAutoAttacksPhase.cs:73)

- On hit: damage is calculated, applied to `target.Stats.Hp`, and `combat.hit`
  is published. (ResolveAutoAttacksPhase.cs:78)

- On miss: `combat.miss` is published with `isFumble` flag.
  (ResolveAutoAttacksPhase.cs:116)

- If HP reaches 0 during a swing, `entity.vital.depleted` (vital = "hp") is
  published immediately and the swing loop breaks. Death handling continues in
  the death pipeline; see death-and-corpses.md.
  (ResolveAutoAttacksPhase.cs:99)

- Status effects tick via `EffectManager.TickPulse()` in
  `ResolveStatusEffectsPhase`. (ResolveStatusEffectsPhase.cs:12)

### Hit and Damage Resolution

- Weapon is determined by the entity's "wield" equipment slot.
  Unarmed if no weapon is equipped. (HitResolver.cs:112)

- Hit roll: `d20 + (attacker.Dexterity - 10) + weapon.hit_bonus`.
  (HitResolver.cs:24)

- Armor class: `10 + (defender.Dexterity - 10) + innate_ac[damageType]
  + sum(equipped_item.ac[damageType])`. Equipment AC is stateless -- reflects
  current wield/wear slots. (HitResolver.cs:40)

- Natural 1 is always a fumble (miss). Natural 20 is always a critical
  (hit regardless of AC). Otherwise hit if total roll >= AC.
  (HitResolver.cs:67)

- Unarmed base damage dice: `1d2+0`. Unarmed damage type: `bash`.
  (HitResolver.cs:8)

- Damage formula: `max(1, floor(dice_result * (1 + (str - 10) * 0.05)))`.
  Minimum 1 after scaling. (HitResolver.cs:91)

- Dice notation: `XdY[+-Z]` parsed by `DiceRoller.Roll`. Malformed notation
  returns 0. (src/Tapestry.Engine/Combat/DiceRoller.cs:10)

- Damage verb for output is looked up by damage amount against a 20-tier
  table in `CombatModule.DamageVerbs`, exposed to pack scripts as
  `tapestry.combat.formatDamageVerb(damage)`, from "tickles" (0+) to
  "VAPORIZES" (421+). (src/Tapestry.Scripting/Modules/CombatModule.cs:241)

  | Min Damage | Verb             |
  |------------|------------------|
  | 421        | VAPORIZES        |
  | 341        | ERADICATES       |
  | 281        | PULVERIZES       |
  | 231        | DESTROYS         |
  | 191        | OBLITERATES      |
  | 156        | ANNIHILATES      |
  | 126        | MASSACRES        |
  | 101        | DISMEMBERS       |
  | 85         | MUTILATES        |
  | 69         | MAIMS            |
  | 55         | devastates       |
  | 43         | decimates        |
  | 33         | mauls            |
  | 25         | wounds           |
  | 18         | injures          |
  | 13         | hits             |
  | 9          | grazes           |
  | 6          | scratches        |
  | 3          | barely scratches |
  | 0          | tickles          |

### Health Tiers

- `HealthTier.Get(hp, maxHp)` maps current HP to a named tier: perfect (100%),
  few scratches (75-99%), small wounds (50-74%), wounded (35-49%),
  badly wounded (20-34%), bleeding profusely (10-19%), near death (<10% or
  maxHp <= 0). (src/Tapestry.Engine/Combat/HealthTier.cs:6)

- Tier and text are sent to clients via GMCP packages `Char.Combat.Target`
  and `Char.Combat.Targets` on engage, hit, kill, and combat-end events.
  (src/Tapestry.Server/Gmcp/Handlers/CharCombatHandler.cs)

### Wimpy / Automatic Flee

- `CheckWimpyPhase` (priority 400) runs after auto-attacks and status effects
  each pulse. (CheckWimpyPhase.cs:7)

- An entity flees automatically if: `wimpy_threshold` property is set to a
  positive integer, current HP% <= threshold, and entity is not in a flee
  cooldown. (CheckWimpyPhase.cs:20)

- Players set their wimpy threshold (0-50%) via the `wimpy` command.
  (packs/@tapestry/core/scripts/combat/commands.js:47)

- The flee cooldown gate prevents chain-fleeing: an entity that just fled will
  not auto-flee again until the cooldown expires, even if HP remains below
  threshold. (CheckWimpyPhase.cs:36; commit 600b00f)

### Flee Mechanics

- `CombatManager.AttemptFlee(entity, context)` handles both player-initiated
  and wimpy-triggered flee. (CombatManager.cs:194)

- Flee is blocked if the entity has the `no_flee` tag; publishes
  `combat.flee.prevented`. (CombatManager.cs:197)

- Flee fails if the current room has no available exits; publishes
  `combat.flee.failed`. (CombatManager.cs:226;
  CombatManagerTests.cs:AttemptFlee_NoExits_Fails)

- On success: entity is removed from all combat, moved to a randomly chosen
  exit room, flee cooldown is set to `currentTick + FleeCooldownTicks`
  (default 80 ticks), movement points are reduced by `FleeMoveCost` (default
  10, floor 0), and `combat.flee` is published.
  (CombatManager.cs:244;
  CombatManagerTests.cs:AttemptFlee_SetsFleeCooldown,
  CombatManagerTests.cs:AttemptFlee_DeductsMovement)

- `FleeCooldownTicks` and `FleeMoveCost` are data-driven via `server.yaml`
  `combat:` section (commit 600b00f; src/Tapestry.Data/ServerConfig.cs:281).

- The flee cooldown also blocks `Engage` from succeeding, so a freshly fled
  entity cannot immediately re-engage. (CombatManager.cs:73)

- Ability pulse delays are cleared on `combat.flee` and `combat.end` so the
  entity's ability queue does not carry over from a fight it is no longer in.
  (src/Tapestry.Server/Modules/CombatEventModule.cs:107)

- The `flee` command is provided by the core pack and only functions when the
  player is in combat. (packs/@tapestry/core/scripts/combat/commands.js:32)

### How Combat Ends

Combat between two entities ends when either:

1. `RemoveFromCombat(A, B)` is called (removes each from the other's list;
   publishes `combat.end` for any side now idle). (CombatManager.cs:141)

2. An entity flees (calls `RemoveEntityFromAllCombat` then teleports the
   entity). (CombatManager.cs:244)

3. An entity's HP reaches 0. `entity.vital.depleted` (vital = "hp") is
   published; `CombatEventModule` calls `HandleEntityDeath`, which calls
   `RemoveEntityFromAllCombat`. The full death pipeline is described in
   death-and-corpses.md. (ResolveAutoAttacksPhase.cs:99;
   CombatEventModule.cs:62; CombatManager.cs:278)

### Pack JS API (`tapestry.combat`)

- `engage(attackerIdStr, targetIdStr)` -- returns `"ok"`, `"no_kill"`,
  `"safe-room"`, `"flee-cooldown"`, `"already-fighting"`, or `"error"`.
  (CombatModule.cs:33)

- `flee(entityIdStr)` -- attempts immediate flee; returns boolean.
  (CombatModule.cs:80)

- `isInCombat(entityIdStr)` -- returns boolean. (CombatModule.cs:108)

- `removeFromAllCombat(entityIdStr)` -- unconditionally removes entity from
  all combat without triggering flee cooldown or movement cost.
  (CombatModule.cs:118)

- `getCombatants(entityIdStr)` -- returns array of opponent ID strings in
  combat-list order. (CombatModule.cs:128)

- `applyDamage(entityIdStr, amount, damageType)` -- subtracts HP directly.
  Death check is deferred to `AbilityResolutionPhase` and
  `ResolveAutoAttacksPhase`. (CombatModule.cs:144)

- `applyAC(entityIdStr, rawDamage, damageType)` -- reduces raw damage by
  `(AC - 10)`, minimum 1. (CombatModule.cs:164)

- `setPrimaryTarget(attackerIdStr, newTargetIdStr)` -- moves an existing
  opponent to front of combat list. (CombatModule.cs:200)

- `savingThrow(entityIdStr, saveType)` -- rolls 1-100 against
  `50 + (wisdom - 10) * 3 + entity.saves`. Returns true if roll > target
  (save succeeds). The `saveType` parameter is accepted but not used in the
  current implementation; differentiation by type is not implemented.
  (CombatModule.cs:210)

- `formatDamageVerb(damage)` -- returns a tagged string for output.
  (CombatModule.cs:184)

- `tapestry.dice.roll(notation)` and `tapestry.dice.rollD20()` expose
  `DiceRoller` directly to pack scripts.
  (src/Tapestry.Scripting/Modules/DiceModule.cs)

### Combat Properties

Registered engine properties on items and NPCs:

| Property          | Type    | Description                                          |
|-------------------|---------|------------------------------------------------------|
| `damage_type`     | String  | Damage type dealt                                    |
| `damage_dice`     | String  | Dice expression (e.g. `2d6+4`)                       |
| `hit_bonus`       | Int     | Bonus to hit roll                                    |
| `combat_name`     | String  | Verb in combat messages                              |
| `wimpy_threshold` | Int     | HP% at which entity auto-flees                       |
| `attack_speed`    | Int     | Attack speed modifier (unused in current auto-attack code) |
| `ac`              | MapInt  | Armor class per damage type                          |

(src/Tapestry.Engine/Combat/CombatProperties.cs)

---

## Rejected and Reverted

- **`killable` tag** (formerly required for an entity to be attackable) was
  removed. The model now inverts: all entities are attackable unless tagged
  `no_kill`. (commit 880e2a8)

- **`CombatManager.Tick()`** was removed as dead code after heartbeat took
  over combat pulse dispatch. Tombstone left in test file:
  `// Tick_ExecutesPhasesInOrder -- removed: CombatManager.Tick() removed`.
  (tests/Tapestry.Engine.Tests/Combat/CombatManagerTests.cs:348)

- **`no_combat` room tag** was consolidated into `safe` during tag naming
  normalization. (commit 2d033d3)

---

## Change Log
