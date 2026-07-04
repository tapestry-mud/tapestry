---
capability: abilities
last-updated: 2026-07-04
---

# Abilities

## Overview

The abilities system is the engine-level pipeline for player and NPC active
skills, spells, and passive combat modifiers. Definitions are registered by
pack scripts and stored in `AbilityRegistry`. Proficiency scores live on
entities (not in the registry). Every active ability invocation is queued onto
the entity and resolved at most once per combat pulse by `AbilityResolutionPhase`,
which runs as a sub-phase of `CombatPulse`. Effects produced by successful hits
are handed off to `EffectManager` (see effects-and-modifiers.md).

Out of scope here: auto-attack damage (combat-resolution.md), character stat growth and
levelling (character-progression.md), the effect lifecycle after
`EffectManager.TryApply` (effects-and-modifiers.md).

---

## Behavior

### Ability Definition Model

- An `AbilityDefinition` carries: `Id`, `Name`, `Type` (Active or Passive),
  `Category` (Skill or Spell), `ResourceCost`, `PulseDelay`, `InitiateOnly`,
  `MaxChance` (default 100), `ProficiencyGainChance` (default 0.05),
  `FailureProficiencyGainMultiplier` (default 0.25), `Variance` (default 100),
  optional `ShortName`, `CommandName`, `AlignmentRange`, `GainStat`,
  `GainStatScale`, `RequiresSlot`, `RequiresSlotTag`, inline `Effect` block,
  `CanTarget` list, and open `Metadata` dictionary.
  (src/Tapestry.Engine/Abilities/AbilityDefinition.cs)

- `AbilityEffectDefinition` (nested under `Effect`) holds `EffectId`,
  `DurationPulses`, a list of `StatModifierDefinition` records, and a
  `Flags` list. (AbilityDefinition.cs)

- `AbilityType` enum: `Active`, `Passive`.
  (src/Tapestry.Engine/Abilities/AbilityType.cs)

- `AbilityCategory` enum: `Skill`, `Spell`.
  (src/Tapestry.Engine/Abilities/AbilityCategory.cs)

### Ability Registry

- `AbilityRegistry` is a case-insensitive in-memory dictionary keyed by
  ability ID. (src/Tapestry.Engine/Abilities/AbilityRegistry.cs)

- `Register(definition)` is an upsert; collision policy (priority, pack
  ordering) is enforced upstream by `RegistrationPolicy` before the write
  reaches the registry. A `RegistrationGate` commit scope is required when
  writing during the sealed phase. (AbilityRegistry.cs; commit 8cc5489)

- Lookup helpers: `Get(id)`, `GetAll()`, `GetByType(type)`,
  `GetByCategory(category)`, `Has(id)`. (AbilityRegistry.cs)

### Pack JS API -- abilities

- Pack scripts register abilities via `tapestry.abilities.register(def)`.
  The call routes through `RegistrationPolicy.Record`, which enforces pack
  isolation and seal rules.
  (src/Tapestry.Scripting/Modules/AbilitiesModule.cs; commit 8cc5489)

- `abilities.learn(entityId, abilityId, options?)` sets initial proficiency
  (default 1, clamped to [1, 100]) and initializes the cap to 25 if not
  already set. (AbilitiesModule.cs; ProficiencyManager.cs:26)

- `abilities.forget(entityId, abilityId)` removes the proficiency entry.

- Proficiency queries: `abilities.getProficiency`, `abilities.setProficiency`,
  `abilities.increaseProficiency`, `abilities.getLearnedAbilities`.

- `abilities.getDefinition(abilityId)` returns a lightweight projection:
  `{id, name, short_name, command_name, type, category, resource_cost,
  pulse_delay, initiate_only, max_chance, has_effect, can_target}`.

- `abilities.queue(entityId, abilityId, targetIdStr)` appends a queued-action
  entry directly onto the entity's `QueuedActions` property. Scripts can use
  this to programmatically queue abilities without a player command.

- `abilities.repeatLast(entityId)` re-queues the ability stored in
  `last_ability_used`.

- On `ability.used`, `AbilitiesModule` dispatches the JS handler stored in
  `definition.Handler`, attributed to the owning pack.
  Handler signature: `(user, target, context)`.
  (AbilitiesModule.cs:69)

### AbilityCommandBridge

- `AbilityCommandBridge.WireAll()` is called post-seal. It iterates all
  Active abilities and registers each as a typeable command at priority 0.
  (src/Tapestry.Engine/AbilityCommandBridge.cs:48)

- The command keyword is `ability.CommandName ?? shortId`, where `shortId` is
  the segment after the last `:` in the ability ID. The full ID is registered
  as an alias. (AbilityCommandBridge.cs:73)

- Visibility is dynamic: the `VisibleTo` predicate re-evaluates proficiency on
  every help/command-list call. A player without proficiency > 0 will not see
  the command; the handler also re-checks and rejects with a message.
  (AbilityCommandBridge.cs:78)

- Pack scripts may shadow auto-generated commands by registering a command at
  priority > 0 with the same keyword (e.g., core `rescue` at priority 1).
  (AbilityCommandBridge.cs:50)

- Target resolution order: explicit name arg from raw input > combat primary
  target > self (only if `can_target` includes "self"). A player argument of
  "self" or "me" always resolves to self. Indexed targeting (`N.name`) is
  supported. (AbilityCommandBridge.cs:147)

- If the resolved target is not self and the attacker is not already in combat,
  `CombatManager.Engage` is called. A flee-cooldown check runs before engage;
  players see "You're too winded from fleeing to attack right now."
  (AbilityCommandBridge.cs:119)

### Proficiency System

- Proficiency is an integer in [1, 100] stored in a per-entity `MapInt`
  property under key `proficiency`. A separate `cap` map stores per-ability
  caps (default 25 on learn; read-back default 100 if key absent).
  (src/Tapestry.Engine/Abilities/ProficiencyManager.cs; AbilityProperties.cs)

- `ProficiencyManager.RollProficiencyGain` formula:
  `effectiveChance = ProficiencyGainChance * (1 - current/100) * gainStatMultiplier`.
  `gainStatMultiplier = 1 + (statValue * GainStatScale)` when `GainStat` is
  set on the definition; otherwise 1.0. On failure, multiply by
  `FailureProficiencyGainMultiplier`. Roll stops at cap.
  (ProficiencyManager.cs:116; commit 1bcb1a5)

- `ProficiencyManager` implements `IQuestProficiencyService` so the quest
  system can award proficiency gains as rewards. (ProficiencyManager.cs:8)

### Passive Ability Processing

- `PassiveAbilityProcessor` provides three entry points consumed by the combat
  pipeline. It never fires autonomously -- callers decide when to invoke.
  (src/Tapestry.Engine/Abilities/PassiveAbilityProcessor.cs)

- `CheckBinaryPassive(entityId, abilityId, random)` -- rolls against
  `proficiency * (variance/100)` when variance < 100, or against
  `proficiency * (maxChance/100)` when variance >= 100. Returns bool.
  Used for one-shot passive checks.
  (PassiveAbilityProcessor.cs:25-27)

- `GetScalingBonus(entityId, abilityId)` -- returns a proportional integer
  bonus from 0 to `max_bonus` (metadata key) based on proficiency/100.

- `GetExtraAttackCount(entityId, random)` -- iterates all passives with
  `metadata.hook == "extra_attack"`, rolls each, and accumulates a count.
  Successful rolls also trigger a proficiency gain roll. Used by
  `ResolveAutoAttacksPhase` (combat-resolution.md).

- `CheckDefensivePassives(entityId, random)` -- iterates passives with
  `metadata.hook == "defensive_check"` and returns the first that succeeds
  its proficiency roll, or null. Used by `ResolveAutoAttacksPhase` (combat-resolution.md).

### Ability Resolution Phase

- `AbilityResolutionPhase` implements `IPulseHandler` but is never registered
  with the heartbeat directly. It is injected into `CombatPulse` and invoked
  as a sub-phase at the start of each combat pulse.
  (src/Tapestry.Engine/Heartbeat/AbilityResolutionPhase.cs:11;
  src/Tapestry.Engine/Heartbeat/CombatPulse.cs:25)

- `CombatPulse` has `Cadence = 20` and is the sole registered pulse handler
  for combat. It is registered via `_heartbeat.Register(_combatPulse)` in
  `TickHandlerModule`. Abilities therefore resolve once per combat pulse.
  `PulseDelay` on an ability definition is denominated in combat pulses.
  (CombatPulse.cs:12; src/Tapestry.Server/Modules/TickHandlerModule.cs:84)

- Each combat pulse: for every entity that has `QueuedActions` set, dequeue
  entries from the front until a valid one is found or the queue is exhausted.
  At most one ability executes per entity per combat pulse.
  (AbilityResolutionPhase.cs:69)

- Invalid queue entry handling (before validation):
  - Null entry (not a `Dictionary<string, object?>`) -- dropped silently.
    (AbilityResolutionPhase.cs:71-75)
  - Entry missing `abilityId` key or with null value -- dropped silently.
    (AbilityResolutionPhase.cs:78-83)
  - Entry whose `abilityId` resolves to no registered definition --
    `ability.fizzled` published with `reason = "unknown_ability"`.
    (AbilityResolutionPhase.cs:85-90)

- Validation order (all checks before execution):
  1. Rest state -- sleeping or resting -> `Asleep`.
     (AbilityResolutionPhase.cs:133)
  2. Alignment range -- if defined and not satisfied -> `Alignment_Restricted`.
     (AbilityResolutionPhase.cs:141)
  3. Proficiency present -- entity must `HasAbility` -> `No_Proficiency`.
     (AbilityResolutionPhase.cs:151)
  4. Equipment slot -- if `RequiresSlot` set, item must be equipped in that
     slot; if `RequiresSlotTag` also set, item must carry that tag ->
     `Equipment_Required`. (AbilityResolutionPhase.cs:157)
  5. `InitiateOnly` -- fizzles if entity is already in combat ->
     `Initiate_Only`. (AbilityResolutionPhase.cs:171)
  6. Target + combat presence for offensive abilities -- `Invalid_Target` or
     `Not_In_Combat`. (AbilityResolutionPhase.cs:177)
  7. Effect gate -- if the ability has an `Effect` block and that effect ID is
     already active on the caster -> `Effect_Present`.
     (AbilityResolutionPhase.cs:194)
  8. Pulse delay -- per-entity, per-ability cooldown stored in an ephemeral
     in-process dictionary -> `Pulse_Delay`. (AbilityResolutionPhase.cs:201)
  9. Resource -- skills cost `movement`; spells cost `resource` (mana). Race
     modifier applied via `RaceCostCalculator.AdjustCost` ->
     `Insufficient_Resources`. (AbilityResolutionPhase.cs:214)

- On any validation failure, `ability.fizzled` is published with `reason` =
  snake_case enum name. (AbilityResolutionPhase.cs:470)

- Offense classification: all Skills are offensive; a Spell is offensive if it
  has no `Effect` block AND carries `damage_dice` in metadata. A Spell with
  `heal_dice` is never offensive. (AbilityResolutionPhase.cs:386)

- On success: deduct resource (race modifier applied) via
  `context.VitalsService.Apply(entity, deductKind, -deductCost, "ability.cost")` -- no
  longer a direct stat write; this publishes `entity.vital.changed` (see events.md) when
  the deduction changes the vital. Also sets `last_ability_used` and records
  `nextReadyPulse = currentPulse + PulseDelay + 1`.
  (AbilityResolutionPhase.cs:241-262)

- Hit roll: if `Variance == 0`, always hit. Otherwise:
  `hitChance = (proficiency * (Variance/100.0)) + (luck * luckScale)`,
  roll d100. Default `luckScale = 0.002`.
  (AbilityResolutionPhase.cs:258)

- Miss path: publishes `ability.missed`, rolls failure proficiency gain.
  (AbilityResolutionPhase.cs:274)

- Hit path: applies `Effect` (if defined) via `EffectManager.TryApply`,
  publishes `ability.used` (carries JS handler reference in data), rolls
  proficiency gain. (AbilityResolutionPhase.cs:295)

- Death check after `ability.used`: if target HP <= 0, publishes
  `entity.vital.depleted` with `vital = "hp"` and `killerId`.
  (AbilityResolutionPhase.cs:324)

- `ClearPulseDelays(entityId)` is available for external callers (e.g., combat
  end / flee events). (AbilityResolutionPhase.cs:55)

---

## Rejected and Reverted

- None on record.

---

## Change Log

- 2026-07-04 [entity-state-mutation-broadcast](changes/2026-07-04-entity-state-mutation-broadcast.md) - ability resource cost is deducted through VitalsService.Apply (reason "ability.cost"), publishing entity.vital.changed instead of a direct stat write
