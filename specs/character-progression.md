---
capability: character-progression
last-updated: 2026-06-12
---

# Character Progression

## Overview

Character progression covers how player and NPC entities gain experience, advance
levels, grow stats, and spend training points. It spans four subsystems:

- **Progression tracks** -- named XP/level containers (ProgressionManager, TrackDefinition)
- **Classes** -- define which track to use, stat growth tables, ability unlock paths
  (ClassDefinition, ClassRegistry, ClassPathProcessor, StatGrowthOnLevelUp)
- **Races** -- define per-stat caps and an ability-cost modifier (RaceDefinition, RaceRegistry)
- **Training** -- lets players spend train points on stats or practice ability tiers
  (TrainingManager, TrainingConfig, CapTier)

All subsystems are engine-generic; no game content is hardcoded. Packs register
definitions via pack JavaScript using the `tapestry.classes`, `tapestry.races`,
`tapestry.progression`, and `tapestry.training` JS namespaces.

Out of scope here: alignment (alignment.md), ability definitions and effects
(abilities-and-effects.md), player persistence (persistence.md), combat outcomes
(combat.md).

---

## Behavior

### Progression Tracks

- A track is the named container for XP and level state. Properties stored on the
  entity are `level` (MapInt by track name) and `xp` (MapInt by track name).
  (src/Tapestry.Engine/Progression/ProgressionProperties.cs:8-14)

- `ProgressionManager.RegisterTrack(TrackDefinition)` registers a track behind the
  registration gate; upsert semantics -- collision resolution is the RegistrationPolicy's
  responsibility before the write lands here.
  (src/Tapestry.Engine/Progression/ProgressionManager.cs:26-29)

- A `TrackDefinition` requires `Name` and `MaxLevel`. XP thresholds can be supplied
  as an `int[]` XpTable (index = level) or an `XpFormula` delegate; table takes
  priority. `GetXpForLevel` returns -1 if neither is configured.
  (src/Tapestry.Engine/Progression/TrackDefinition.cs:6-31)

- A track optionally carries a `DeathPenalty` double and an `OnLevelUp` callback,
  both supplied by the registering pack.
  (src/Tapestry.Engine/Progression/TrackDefinition.cs:11-12)

- When an entity is first queried on a track, `GetLevel` auto-initializes level to 1
  and XP to 0.
  (src/Tapestry.Engine/Progression/ProgressionManager.cs:56-64)

- `GrantExperience` adds XP, fires `progression.xp.gained`, then advances level as
  many times as the new XP total crosses the next threshold. Level advances stop at
  `MaxLevel`; each advance fires `progression.level.up` with `track`, `oldLevel`,
  `newLevel`, and `entityId` in the payload.
  (src/Tapestry.Engine/Progression/ProgressionManager.cs:113-177)

- `DeductExperience` reduces XP but floors at the threshold for the current level
  (never de-levels) and fires `progression.xp.lost`.
  (src/Tapestry.Engine/Progression/ProgressionManager.cs:179-223)

- `ResetTrack` hard-sets level to 1 and XP to 0 and fires `progression.track.reset`.
  (src/Tapestry.Engine/Progression/ProgressionManager.cs:225-251)

- `progression.calculateMobXp(killerLevel, mobLevel, baseXp)` -- if killer is 10+
  levels above mob, returns 0; otherwise scales linearly by 10% per level advantage
  (minimum 1). (src/Tapestry.Scripting/Modules/ProgressionModule.cs:190-203)

- `progression.groupShare(combatantCount)` -- returns a share multiplier of
  `1.0 / (n * 0.6 + 0.4)` for n > 1, or 1.0 for solo.
  (src/Tapestry.Scripting/Modules/ProgressionModule.cs:205-212)

---

### Classes

- A `ClassDefinition` carries: `Id`, `Name`, `Track` (the progression track name),
  `StatGrowth` (dict of StatType to dice string), `GrowthBonuses` (dict of vital stat
  to attribute stat), `Path` (list of level-gated ability unlocks), `TrainsPerLevel`
  (default 5), `AllowedCategories`, `AllowedGenders`, `StartingAlignment`, and
  display fields. (src/Tapestry.Engine/Classes/ClassDefinition.cs:5-23)

- `ClassRegistry.GetEligibleClasses(raceCategory, gender)` returns classes where
  `AllowedCategories` is empty or contains the category, AND `AllowedGenders` is
  empty or contains the gender (both case-insensitive).
  (src/Tapestry.Engine/Classes/ClassRegistry.cs:41-46)

- `ClassPathEntry` is a record of `(Level, AbilityId, UnlockedVia?)` defined at
  ClassPathEntry.cs:3. `ClassPathEntry.UnlockedVia` is write-only -- no engine code
  reads or enforces it (ClassPathEntry.cs:3). It is stored but has no effect.

- `ClassPathProcessor.GrantPathEntries` skips entries where `UnlockedVia` is non-null
  or empty (ClassPathProcessor.cs:78). This means level-up and character-creation
  grants respect the field.

- `setClass` (ClassesModule.cs:253-259) back-grants path entries without checking
  `UnlockedVia` -- unlike ClassPathProcessor, which respects it. This means
  quest-gated path entries can be leaked via setClass.

- On `character.created`, `ClassPathProcessor` grants all path entries at level 1
  that have no `UnlockedVia`.
  (src/Tapestry.Engine/Classes/ClassPathProcessor.cs:58-71)

- On `progression.level.up`, `ClassPathProcessor` listens only for the track that
  matches `classDef.Track`; it grants path entries whose `Level` equals the new level.
  (src/Tapestry.Engine/Classes/ClassPathProcessor.cs:35-55)

- `classes.setClass(entityId, classId)` also back-grants all path entries at or below
  the entity's current level on that track.
  (src/Tapestry.Scripting/Modules/ClassesModule.cs:248-259)

- Packs register classes via `tapestry.classes.register({...})`. The JS fields map
  directly to `ClassDefinition` properties; `stat_growth` keys and `growth_bonuses`
  keys are parsed with underscores stripped before `Enum.TryParse<StatType>`.
  (src/Tapestry.Scripting/Modules/ClassesModule.cs:62-74, 123-138)

- Example-pack warrior: track "combat", stat_growth max_hp "2d6+2" + constitution
  bonus, max_movement "1d4" + dexterity bonus, 5 trains/level.
  (packs/@tapestry/example-pack/scripts/classes/warrior.js)

- Example-pack mage: track "magic", stat_growth max_hp "1d6" + constitution bonus,
  max_resource "2d4+1" + intelligence bonus, 5 trains/level.
  (packs/@tapestry/example-pack/scripts/classes/mage.js)

---

### Stat Growth on Level-Up

- `StatGrowthOnLevelUp.Subscribe` subscribes to `progression.level.up`. For each
  `StatGrowth` entry on the class, it rolls the dice string via `DiceRoller.Roll`,
  optionally adds a bonus of `max(0, (attrValue - 10) / 2)` if `GrowthBonuses`
  maps the stat to a source attribute, then applies the total delta to the
  corresponding `Base*` field on `entity.Stats`.
  (src/Tapestry.Engine/Classes/StatGrowthOnLevelUp.cs:14-38)

- Supported vital stats for growth: Strength, Intelligence, Wisdom, Dexterity,
  Constitution, Luck, MaxHp, MaxResource, MaxMovement.
  (src/Tapestry.Engine/Classes/StatGrowthOnLevelUp.cs:53-66)

- After applying stat deltas, `TrainingManager.GrantTrains(entityId, def.TrainsPerLevel)`
  is called on the same level-up event.
  (src/Tapestry.Engine/Classes/StatGrowthOnLevelUp.cs:38)

---

### Races

- A `RaceDefinition` carries: `Id`, `Name`, `RaceCategory` (used for class
  eligibility filtering), `StatCaps` (dict of StatType to int cap), `CastCostModifier`
  (int, added to base ability cost; floored at 0 by `RaceCostCalculator`),
  `RacialFlags` (list of strings), `StartingAlignment`.
  (src/Tapestry.Engine/Races/RaceDefinition.cs:5-19)

- `RaceCostCalculator.AdjustCost(baseCost, race)` adds `race.CastCostModifier` to the
  base cost and floors at 0.
  (src/Tapestry.Engine/Races/RaceCostCalculator.cs:5-11)

- `RaceRegistry` is case-insensitive for ID lookups. Registration goes through
  `RegistrationPolicy` before reaching the registry (upsert, no internal arbitration).
  (src/Tapestry.Engine/Races/RaceRegistry.cs:7-38)

- `races.getStatCap(raceId, statName)` returns the race's cap for that stat or 25 as
  default if the race or stat is unknown.
  (src/Tapestry.Scripting/Modules/RacesModule.cs:165-173)

- Example races: human (cast_cost_modifier -10, balanced caps up to 25) and elf
  (cast_cost_modifier -15, high int/dex/wis caps, lower str/con caps).
  (packs/@tapestry/example-pack/scripts/races/human.js,
   packs/@tapestry/example-pack/scripts/races/elf.js)

---

### Training System

- Training points are stored in the `trains_available` (int) entity property, which
  applies only to `Player` entities.
  (src/Tapestry.Engine/Training/TrainingProperties.cs:8-15)

- `TrainingManager.TryTrain(entityId, stat)` deducts one train, increments the
  `Base*` stat field by 1, and calls `entity.Stats.Invalidate()`. It fails with
  `AtRaceCap` if the current stat value has reached the race's cap (default cap 25
  if race is not registered or has no entry for that stat).
  (src/Tapestry.Engine/Training/TrainingManager.cs:117-165)

- If `TrainingConfig.RequireSafeRoomForStats` is true (default false), `TryTrain`
  requires the entity's room to carry the `safe` tag.
  (src/Tapestry.Engine/Training/TrainingManager.cs:125-133)

- `TrainingConfig.TrainableStats` (default: all six core attributes) gates which stats
  `TryTrain` accepts; packs can narrow or expand this list via
  `training.setTrainable(stat, enabled)`.
  (src/Tapestry.Engine/Training/TrainingConfig.cs:6-27)

- Ability caps follow the `CapTier` enum: Novice = 25, Apprentice = 50,
  Journeyman = 75, Master = 100.
  (src/Tapestry.Engine/Training/CapTier.cs:3-9)

- `TryPractice(entityId, abilityId)` raises the ability cap by exactly one tier.
  It requires: (1) the entity has learned the ability, (2) a trainer with tag
  `skill_trainer` is in the same room, (3) the trainer's config lists the ability,
  (4) the trainer's tier is exactly one step above the current cap (no tier skipping).
  If proficiency is below the old cap, it is boosted by `TrainingConfig.CatchUpBoost`
  (default 5) up to the old cap.
  (src/Tapestry.Engine/Training/TrainingManager.cs:65-114)

- `TrainerConfig` is a record of `(CapTier Tier, IReadOnlyList<string> AbilityIds)`.
  It is promoted to a typed field on the entity (not stored in the property bag).
  (src/Tapestry.Engine/Training/TrainerConfig.cs:3;
   commit 9f66711 -- "promote DispositionDefinition, TrainerConfig, ShopConfig out of property bag into typed fields")

- `training.findTrainerInRoom(entityId)` exposes trainer discovery to pack scripts;
  returns `{ id, name, tier, abilities }` or null.
  (src/Tapestry.Scripting/Modules/TrainingModule.cs:88-100)

---

## Rejected and Reverted

No tombstones. No behavior in this area has been shipped and then reverted as of
the examined history.

---

## Change Log

| Change Record | Summary |
|---------------|---------|
