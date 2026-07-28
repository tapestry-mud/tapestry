---
capability: combat-resolution
last-updated: 2026-07-27
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

- On hit: damage is calculated and applied via
  `VitalsService.Apply(target, VitalKind.Hp, -damage, "combat.melee")` -- the `Stats.Hp`
  write is no longer direct. This publishes `entity.vital.changed` (see events.md) when the
  clamped value changes; `combat.hit` is published separately for combat text/output.
  (ResolveAutoAttacksPhase.cs:85-103)

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

- Damage dice source precedence: (1) the wielded weapon's `damage_dice` property
  if a weapon is equipped; (2) the attacker's own `damage_dice` property if set
  (mobs/NPCs carry this to express their natural attack table); (3) the unarmed
  default `1d2+0`. This means an unarmed mob with `damage_dice: "3d6"` hits per
  its template, not the unarmed default.
  (HitResolver.cs:101; HitResolverTests.cs:GetWeaponDamageDice_*)

- Unarmed base damage dice: `1d2+0`. Unarmed damage type: `bash`.
  (HitResolver.cs:8)

- Damage formula: `max(1, floor(dice_result * (1 + (str - 10) * 0.05)))`.
  Minimum 1 after scaling. (HitResolver.cs:91)

- Dice notation: `XdY[+-Z]` parsed by `DiceRoller.Roll`. Malformed notation
  returns 0. (src/Tapestry.Engine/Combat/DiceRoller.cs:10)

- Damage verb for output is looked up by damage amount against a 20-tier
  table in `CombatModule.DamageVerbs`, exposed to pack scripts as
  `tapestry.combat.formatDamageVerb(damage)`, from "tickles" (0+) to
  "VAPORIZES" (421+). Design intent (2026-07-04 retune): verbs key on
  ABSOLUTE damage - the progression channel. A geared level-1 hit (~6-7)
  reads "grazes"/"hits"; the decorated top tiers stay gear/spell territory.
  Relative target state is a separate channel (the condition line in
  @tapestry/core combat output). Boundaries are pinned by
  DamageVerbLadderTests. (src/Tapestry.Scripting/Modules/CombatModule.cs:339)

  | Min Damage | Verb             |
  |------------|------------------|
  | 421        | VAPORIZES        |
  | 301        | ERADICATES       |
  | 241        | PULVERIZES       |
  | 191        | DESTROYS         |
  | 146        | OBLITERATES      |
  | 116        | ANNIHILATES      |
  | 91         | MASSACRES        |
  | 73         | DISMEMBERS       |
  | 59         | MUTILATES        |
  | 47         | MAIMS            |
  | 37         | devastates       |
  | 29         | decimates        |
  | 22         | mauls            |
  | 17         | wounds           |
  | 13         | injures          |
  | 9          | hits             |
  | 6          | grazes           |
  | 4          | scratches        |
  | 2          | barely scratches |
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
  each pulse; it delegates its per-combatant decision to
  `CombatManager.ShouldFlee`. (CheckWimpyPhase.cs:16; CombatManager.cs:ShouldFlee)

- `ShouldFlee` is the single flee predicate shared by `CheckWimpyPhase` (players
  and NPCs, pulse-driven) and `MobAIManager.TryFlee` (mob AI tick), so both
  trigger points agree on one decision instead of duplicating threshold math.
  (CombatManager.cs:ShouldFlee; MobAIManager.cs:345-367)

- An entity flees automatically if: `wimpy_pct` property is set to a positive
  integer (0-100 scale), current HP% <= that value, entity HP and max HP are
  both positive, and entity is not in a flee cooldown. The legacy
  `wimpy_threshold` and `flee_threshold` keys are retired and never consulted.
  (CombatManager.cs:ShouldFlee)

- Players set their wimpy threshold (0-50%) via the `wimpy` command.
  (packs/@tapestry/core/scripts/combat/commands.ts:47)

- The flee cooldown gate prevents chain-fleeing: an entity that just fled will
  not auto-flee again until the cooldown expires, even if HP remains below
  threshold. (CombatManager.cs:ShouldFlee; commit 600b00f)

### Flee Mechanics

- `CombatManager.AttemptFlee(entity, context)` handles both player-initiated
  and wimpy-triggered flee. (CombatManager.cs:194)

- Flee is blocked if the entity has the `no_flee` tag; publishes
  `combat.flee.prevented`. (CombatManager.cs:197)

- Flee fails if the current room has no available exits; publishes
  `combat.flee.failed`. (CombatManager.cs:226;
  CombatManagerTests.cs:AttemptFlee_NoExits_Fails)

- Exit selection prefers destinations that are not tagged `no_wander`
  (EngineTags.NoWander): the exit list is filtered to exclude exits whose
  target room carries the tag, and the random pick is made from that filtered
  set. If every exit leads to a `no_wander` room (or the room has no exits at
  all), the filter falls back to the unfiltered exit list rather than
  stranding a cornered mob -- "prefer not to enter," not "never flee if
  every path leads to one." (CombatManager.cs:227-252;
  CombatManagerTests.cs:AttemptFlee_AvoidsNoWanderDestination,
  CombatManagerTests.cs:AttemptFlee_FallsBackToNoWanderRoomWhenNoOtherExit)

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

- `applyDamage(entityIdStr, amount, damageType)` -- applies damage via
  `VitalsService.Apply(entity, VitalKind.Hp, -amount, "ability.damage")`; the
  `Stats.Hp` write is no longer direct. Death check is deferred to
  `AbilityResolutionPhase` and `ResolveAutoAttacksPhase`.
  (CombatModule.cs:171-184)

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
| `wimpy_pct`       | Int     | HP% (0-100) at which entity auto-flees                |
| `attack_speed`    | Int     | Attack speed modifier (unused in current auto-attack code) |
| `ac`              | MapInt  | Armor class per damage type                          |

(src/Tapestry.Engine/Combat/CombatProperties.cs)

### Swell Combat (Boss Slice 1)

The swell loop is an embedded beat in the existing fight, not a separate mode.
Any in-combat entity carrying a non-empty `swell_window` dial is a swell boss;
all timing, content, and magnitude levers are read off the mob's properties.

- `SwellClockManager` is a per-fight state machine with phases
  Baseline -> Telegraph -> Window -> Resolve, advanced every tick.
  (src/Tapestry.Engine/Combat/SwellClockManager.cs:10;
  SwellClockManager.cs:33) Telegraph locks one of the boss's two attack lines and
  emits a decelerating stretch wind-up; the `tell` dial chooses how much the
  wind-up reveals (full / shape / hidden). (SwellClockManager.cs:131)

- The window opens for the boss's `swell_window_ticks`; the first committed
  battle counter verb triggers validate + resolve, and a timeout with nothing
  committed is a weather. (SwellClockManager.cs:193; SwellClockManager.cs:249)

- A swell suspends the fight's combat actions, not just the auto-attack. The
  auto-attack phase skips a suppressed fight, and the ability-resolution phase
  freezes a queued ability for an actor in an active swell, so neither weapon
  swings nor a queued cast/bash leak damage mid-swell.
  (src/Tapestry.Engine/Combat/ResolveAutoAttacksPhase.cs:39;
  src/Tapestry.Engine/Heartbeat/AbilityResolutionPhase.cs:65;
  SwellClockManager.cs:207)

- The validate step runs a deterministic pack-registered validator looked up by
  the boss's `swell_window` dial; `CombatContext` is the read-only snapshot it
  receives and `ValidationResult` (Countered | Whiffed | Weathered) is what it
  returns. (src/Tapestry.Engine/Combat/WindowValidatorRegistry.cs:8;
  src/Tapestry.Engine/Combat/CombatContext.cs:24)

- `resolve` is the single mutator. It maps the outcome to a clamped HP change
  read from the boss dials - countered chunks the boss by `swell_chunk_pct`,
  whiffed/weathered hit the player by `swell_whiff_pct` / `swell_weather_pct` -
  applies it via `VitalsService.Apply(victim, VitalKind.Hp, -amount, "combat.swell")`,
  and publishes the existing `entity.vital.depleted` on a lethal result, reusing
  the death pipeline.
  (SwellClockManager.cs:269; SwellClockManager.cs:314; SwellClockManager.cs:345-361)

- Swell beats render to the player through a
  `combat.swell.telegraph` / `combat.swell.window` / `combat.swell.resolve`
  event trio. (src/Tapestry.Server/Modules/SwellEventModule.cs:22)

- The swell arc holds the command prompt so it reads as one continuous beat. The event
  module opens a prompt hold (see output-pipeline's Prompt hold) on
  `combat.swell.telegraph` - idempotent across the decel beats - and releases it on
  `combat.swell.resolve` after the outcome narration renders, so the tick-end flush does one
  clean redraw and the player is back at a baseline prompt. Baseline is not held; the hold is
  surgical to Telegraph -> Resolve.
  (src/Tapestry.Server/Modules/SwellEventModule.cs:37-47)

- A fight dropped before ever reaching Resolve - the boss killed by something outside the
  swell, or the player disengaging - publishes `combat.swell.abandoned` from the stale-fight
  cleanup so the held prompt is released even with no Resolve. The event reads the player id
  off the fight state directly, since the cleanup allows the boss entity to be already gone;
  the module releases the hold on it without rendering.
  (src/Tapestry.Engine/Combat/SwellClockManager.cs:55-73;
  src/Tapestry.Server/Modules/SwellEventModule.cs:27,52-58)

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

- 2026-07-27 [flee-avoids-no-wander](changes/2026-07-27-flee-avoids-no-wander.md) - `CombatManager.AttemptFlee` now prefers exits whose destination room isn't tagged `no_wander` (falls back to the unfiltered exit list if every exit leads to one); `no_wander` promoted to an engine tag (`EngineTags.NoWander`) with a new `tapestry.world.addRoomTag` write binding so packs can apply it at runtime
- 2026-07-22 [prompt-hold-gate](changes/2026-07-22-prompt-hold-gate.md) - the swell arc holds the command prompt (Telegraph -> Resolve) so it reads as one beat; new `combat.swell.abandoned` event releases the hold for fights dropped before Resolve
- 2026-07-04 [damage-verb-ladder-retune](changes/2026-07-04-damage-verb-ladder-retune.md) - DamageVerbs MinDamage boundaries retuned for the low-level damage economy (geared level-1 hits read grazes/hits, good rolls injures); verbs key on absolute damage as the progression channel; boundaries pinned by DamageVerbLadderTests
- 2026-07-04 [entity-state-mutation-broadcast](changes/2026-07-04-entity-state-mutation-broadcast.md) - auto-attack, swell resolve, and pack applyDamage HP writes route through VitalsService (publishing entity.vital.changed); combat.hit still fires separately for combat text
- 2026-07-03 [vocabulary-consolidation](changes/2026-07-03-vocabulary-consolidation.md) - flee unified onto one wimpy_pct property read by a single CombatManager.ShouldFlee predicate; flee_threshold/wimpy_threshold retired
- 2026-06-21 [boss-combat-slice-1](changes/2026-06-21-boss-combat-slice-1.md) - the embedded swell loop: per-fight swell clock, the combat-action gate (auto-attack AND ability resolution suspended during a swell), the validate/resolve seam, resolve as the single HP mutator reusing the existing death event
