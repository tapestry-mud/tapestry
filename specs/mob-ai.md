---
capability: mob-ai
last-updated: 2026-07-03
---

# Mob AI

## Overview

The mob AI system coordinates NPC behavior dispatch, disposition/aggro, and combat
gating. Key engine classes: `MobAIManager` (tick sweep, budget, quarantine),
`DispositionEvaluator` (alignment-based reactions), and `MobCommandQueue` (delayed
command sequencing). Pack scripts register named behaviors via the JS
`mobs.registerBehavior` API. Spawning, stat derivation, and loot tables are out of
scope here (see mob-lifecycle.md). Combat round resolution is out of scope (see
combat-resolution.md).

## Behavior

### Area activation and the tick sweep

- Areas activate when a player enters any room in that area (prefix = segment before the
  first colon in a room ID); they deactivate when the last player leaves.
  (src/Tapestry.Engine/Mobs/MobAIManager.cs:103-126)
- `MobAIManager.Tick()` sweeps NPCs from rooms in active areas only. Mobs in inactive
  areas receive no behavior dispatch, no `mob.ai.tick` event, and no disposition check.
  (src/Tapestry.Engine/Mobs/MobAIManager.cs:143-147;
  tests/Tapestry.Engine.Tests/Mobs/MobAIManagerTests.cs:Tick_SkipsBehavior_ForDormantAreaMobs)
- The sweep reads room contents, not the entity tag index. An NPC in a room of an active
  area is processed even if it is absent from the world tag index.
  (tests/Tapestry.Engine.Tests/Mobs/MobAIManagerTests.cs:Tick_SweepsRoomContents_NotTagIndex)

### Budget and cursor

- Per-tick wall-clock budget defaults to 25 ms (`MobAiSection.TickBudgetMs`). Budget is
  checked between mobs; at least one mob is always processed per tick (progress guarantee).
  (src/Tapestry.Engine/Mobs/MobAIManager.cs:154-178;
  tests/Tapestry.Engine.Tests/Mobs/MobAIManagerTests.cs:Tick_AlwaysProcessesAtLeastOneMob_EvenOverBudget)
- When budget is exhausted mid-sweep a cursor (last-processed entity ID) resumes from the
  next entity ID on the following tick. `CurrentCursorLag` is the deferred count (0 after
  a full sweep). If the cursor entity despawns, the sweep resumes from the next ID above
  without error or double-visit.
  (src/Tapestry.Engine/Mobs/MobAIManager.cs:156-184;
  tests/Tapestry.Engine.Tests/Mobs/MobAIManagerTests.cs:Tick_BudgetExhausted_DefersMobs_AndCursorResumes,
  Tick_CursorToleratesDespawnBetweenTicks)
- Four cost phases are recorded as metrics (`tapestry.mob_ai.phase_ms`):
  scan, behavior, publish, disposition.
  (src/Tapestry.Engine/Mobs/MobAIManager.cs:186-189;
  tests/Tapestry.Engine.Tests/Mobs/MobAIManagerTests.cs:Tick_RecordsMobAiPhaseMetrics)

### Posture gate

- Mobs with `rest_state` == "resting" or "sleeping" skip behavior dispatch and the
  `mob.ai.tick` publish entirely. Sleeping mobs additionally skip disposition evaluation.
  (src/Tapestry.Engine/Mobs/MobAIManager.cs:207-211;
  src/Tapestry.Engine/Mobs/DispositionEvaluator.cs:59-62;
  tests/Tapestry.Engine.Tests/Mobs/MobAIManagerTests.cs:Tick_SleepingMob_SkipsBehavior,
  Tick_RestingMob_SkipsBehavior, Tick_SleepingMob_SkipsMobAiTickPublish)

### Behavior plugin system

- `MobAIManager.RegisterBehavior(name, handler, owner?)` stores a named C# delegate.
  Handler receives `MobContext` (entityId, name, roomId, behavior).
  (src/Tapestry.Engine/Mobs/MobAIManager.cs:65-70)
- From JS, packs call `mobs.registerBehavior(name, fn, options?)`. The optional
  `{ override: true }` flag is required to replace another pack's behavior; omitting it
  when a collision exists produces a boot error via the registration policy.
  (src/Tapestry.Scripting/Modules/MobsModule.cs:52-95)
- Each JS behavior invocation is guarded by `MobInvocationBudget.Arm(InvocationCapMs)`
  (default 50 ms). Exceeding the cap throws `MobBudgetExceededException`.
  (src/Tapestry.Scripting/Modules/MobsModule.cs:89-93)

### Behavior quarantine

- A behavior accumulates a strike when its handler exceeds the invocation cap (by wall
  clock) or throws `MobBudgetExceededException`. Ordinary exceptions do not strike.
  (src/Tapestry.Engine/Mobs/MobAIManager.cs:244-250;
  tests/Tapestry.Engine.Tests/Mobs/MobAIManagerTests.cs:Behavior_OrdinaryException_DoesNotStrike)
- After `QuarantineStrikes` (default 3) strikes the behavior is quarantined for the
  server lifetime. Quarantined handlers are silently skipped; `mob.ai.tick` still fires.
  (src/Tapestry.Engine/Mobs/MobAIManager.cs:301-310;
  tests/Tapestry.Engine.Tests/Mobs/MobAIManagerTests.cs:Behavior_QuarantinedAfterThreeStrikes_PublishStillFires)
- Config defaults: `TickBudgetMs=25`, `InvocationCapMs=50`, `QuarantineStrikes=3`.
  (src/Tapestry.Data/ServerConfig.cs:237-239;
  tests/Tapestry.Engine.Tests/Mobs/MobAIManagerTests.cs:MobAiSection_Defaults_AreGroundedValues)

### Built-in behaviors (core pack)

- **stationary** -- empty handler; registered as a no-op placeholder.
  (packs/@tapestry/core/scripts/mobs/behaviors.js:2-4)
- **wander** -- waits `wander_interval` ticks (default 30), rolls `wander_chance`
  (default 0.3), then moves to a random adjacent exit. Resolves the target room for the
  chosen exit and skips if that room is tagged `no_wander`. Also skips exits leaving the
  current area when `wander_boundary` == "area" (default). Does not run while in combat.
  (packs/@tapestry/core/scripts/mobs/behaviors.js:7-54)
- **patrol** -- follows an ordered `patrol_route` room-ID list, bouncing direction at
  each end. Waits `patrol_interval` (default 30) and rolls `patrol_chance` (default 0.5).
  State persisted as `_patrol_index` / `_patrol_direction` entity properties.
  Does not run while in combat.
  (packs/@tapestry/core/scripts/mobs/behaviors.js:57-123)
- Hostility is NOT a behavior. A mob combines movement behavior with `base_disposition:
  hostile`; aggro is entirely disposition-driven, orthogonal to movement.
  (packs/@tapestry/core/scripts/mobs/behaviors.js:125-128)

### Combatant logic (battle_commands)

- Any mob with a non-empty `battle_commands` list dispatches a random command during
  combat via `mob.ai.tick`, regardless of behavior name. This makes combat abilities
  composable with any movement behavior.
  (packs/@tapestry/core/scripts/mobs/behaviors.js:133-161)
- Timing: `battle_interval` (default 15 ticks) and `battle_chance` (default 0.4) are
  read from entity properties at runtime; JS defaults live in behaviors.js:146,152.
  An empty string in the list is a deliberate noop (auto-attack only on that roll).
  (packs/@tapestry/core/scripts/mobs/behaviors.js:146-158;
  src/Tapestry.Engine/Mobs/MobProperties.cs:28-29)

### Idle commands

- Mobs with `idle_commands` fire a random command when not in combat, governed by
  `idle_interval` (default 30 ticks) and `idle_chance` (default 0.3).
  (packs/@tapestry/core/scripts/mobs/idle.js:1-27;
  src/Tapestry.Engine/Mobs/MobProperties.cs:21-22)

### Mob command dispatch

- `mobs.command(entityId, cmd, delay?)` enqueues commands in `MobCommandQueue`.
  Semicolon-separated segments chain with zero additional delay after the first.
  Commands execute at their fire tick via `CommandRouter.RouteForMob`.
  (src/Tapestry.Scripting/Modules/MobsModule.cs:187-205;
  src/Tapestry.Engine/Mobs/MobCommandQueue.cs:29-43)
- Core pack registers `say` and `emote` as mob commands; both suppress output when no
  players are in the room.
  (packs/@tapestry/core/scripts/mobs/commands.js:1-20)

### Mob script hooks

- `mobs.registerScript(templateId, hooks)` registers a per-template hooks object.
  (src/Tapestry.Scripting/Modules/MobsModule.cs:208-212)
- Core pack dispatches three hooks: `onSay` (triggered by `player.say`), `onAttack`
  (triggered by `combat.engage` targeting the NPC), `onDeath` (triggered by `mob.death`
  after corpse creation -- the mob entity is already removed by this point). All hooks
  run under the same invocation cap as behaviors.
  (packs/@tapestry/core/scripts/mobs/onsay-dispatch.js,
  onattack-dispatch.js, ondeath-dispatch.js;
  src/Tapestry.Scripting/Modules/MobsModule.cs:214-248)

### Disposition and aggro evaluation

- `Disposition` enum: Neutral, Friendly, Hostile. (src/Tapestry.Engine/Disposition.cs:3-7)
- Per-tick, `DispositionEvaluator.EvaluateForMob` runs for every player in rooms where a
  mob has `Disposition==Hostile` or a `DispositionRules` block. Neutral/no-rules mobs
  are never evaluated.
  (src/Tapestry.Engine/Mobs/MobAIManager.cs:273-285;
  src/Tapestry.Engine/Mobs/DispositionEvaluator.cs:46-56)
- A per-tick cache deduplicates (mob, player) pairs within a tick. A persistent
  reaction-state dict suppresses re-firing unchanged non-hostile reactions. Hostile always
  re-dispatches (Engage is idempotent via its duplicate guard).
  (src/Tapestry.Engine/Mobs/DispositionEvaluator.cs:14-18, 83-91)
- Reaction-state clears for a player on `player.moved`, so reactions fire fresh on the
  next room entry.
  (src/Tapestry.Engine/Mobs/DispositionEvaluator.cs:28-34)
- Rules evaluation: each `DispositionRule` tests `MinAlignment`, `MaxAlignment`,
  `Buckets`, and `HasTag`; first match wins; no match falls to `Default`.
  (src/Tapestry.Engine/Mobs/DispositionEvaluator.cs:114-126;
  src/Tapestry.Engine/Mobs/DispositionDefinition.cs)
- Events dispatched: `mob.aggro` (hostile), `mob.disposition.wary`, `mob.disposition.friendly`.
  (src/Tapestry.Engine/Mobs/DispositionEvaluator.cs:149-200)
- **Admin exemption:** hostile reactions are suppressed for players with the "admin" role.
  (src/Tapestry.Engine/Mobs/DispositionEvaluator.cs:65-68)
- **Sleeping mob exemption:** `rest_state` == "sleeping" suppresses disposition entirely.
  Resting mobs remain aware.
  (src/Tapestry.Engine/Mobs/DispositionEvaluator.cs:59-62)
- **Safe rooms:** `CombatManager.Engage` refuses to open combat in rooms tagged "safe",
  so aggro fired in a safe room is a no-op. The evaluator itself does not check safe rooms.
  (src/Tapestry.Engine/Combat/CombatManager.cs:64-70)

### Room-entry aggro ordering

- `OnPlayerEnteredRoom` calls `EvaluateRoom(aggroOnly:true)` inside `MoveEntity` so
  hostile mobs engage before the room description is sent.
  (src/Tapestry.Engine/Mobs/MobAIManager.cs:320-324)
- A deferred JS call to `TriggerDisposition` (after room description) handles friendly/
  wary reactions.
  (src/Tapestry.Engine/Mobs/MobAIManager.cs:326-331)
- `OnMobEnteredRoom` evaluates all present players immediately (`aggroOnly:false`).
  (src/Tapestry.Engine/Mobs/MobAIManager.cs:333-343)

### Flee

- A mob with `wimpy_pct` > 0 (0-100 scale) that is in combat at or below that HP%
  attempts to flee on its AI tick turn via `CombatManager.ShouldFlee` then
  `CombatManager.AttemptFlee`. This is the same predicate `CheckWimpyPhase` uses for
  players, so mob and player auto-flee share one decision. A flee cooldown prevents
  chain-fleeing; on cooldown the mob stands and fights.
  (src/Tapestry.Engine/Mobs/MobAIManager.cs:345-367;
  src/Tapestry.Engine/Combat/CombatManager.cs:ShouldFlee;
  tests/Tapestry.Engine.Tests/Mobs/MobAIManagerTests.cs:Tick_WimpyMobOnFleeCooldown_StandsAndFights)
- When a mob succeeds in fleeing (`TryFlee` returns true), the entire behavior dispatch
  block is skipped for that tick, including the `mob.ai.tick` publish. The posture gate
  and `TryFlee` share the same outer condition (MobAIManager.cs:212), so a fleeing mob
  receives neither a behavior dispatch nor a `mob.ai.tick` event on that tick.
  (src/Tapestry.Engine/Mobs/MobAIManager.cs:212-271)

## Rejected and Reverted

- **aggro behavior (reverted):** An `aggro` named behavior was shipped in commit a2dd1be
  (packs/tapestry-core/scripts/mobs/behaviors.js:126-142 in the original monorepo layout).
  It scanned the room for players, checked the `safe` tag, and published `mob.aggro`
  directly. It was retired when the packs directory was extracted to the tapestry-packs
  sibling repo (engine commit f617153) and was not carried forward; the current behaviors.js
  in packs/@tapestry/core replaces it with the comment "Aggro is NOT a behavior" and the
  DispositionEvaluator alignment-based system handles all aggro dispatch. Engine commit
  53b4b28 further hardened disposition by adding the admin exemption.

## Change Log
- 2026-07-03 [vocabulary-consolidation](changes/2026-07-03-vocabulary-consolidation.md) - the mob-AI flee path routes through CombatManager.ShouldFlee reading wimpy_pct
