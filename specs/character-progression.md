---
capability: character-progression
last-updated: 2026-06-12
---

# Character Progression

## Overview

Character progression covers how player and NPC entities gain experience, advance
levels, grow stats, and spend training points. It spans five subsystems:

- **Progression tracks** -- named XP/level containers (ProgressionManager, TrackDefinition)
- **Classes** -- define which track to use, stat growth tables, ability unlock paths
  (ClassDefinition, ClassRegistry, ClassPathProcessor, StatGrowthOnLevelUp)
- **Races** -- define per-stat caps and an ability-cost modifier (RaceDefinition, RaceRegistry)
- **Training** -- lets players spend train points on stats or practice ability tiers
  (TrainingManager, TrainingConfig, CapTier)
- **Alignment** -- a numeric [-1000, 1000] moral axis that buckets into evil/neutral/good
  (AlignmentManager, AlignmentConfig)

All subsystems are engine-generic; no game content is hardcoded. Packs register
definitions via pack JavaScript using the `tapestry.classes`, `tapestry.races`,
`tapestry.progression`, `tapestry.training`, and `tapestry.alignment` JS namespaces.

Out of scope here: ability definitions and effects (abilities-and-effects.md),
player persistence (persistence.md), combat outcomes (combat.md).

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

- `ClassPathEntry` is a record of `(Level, AbilityId, UnlockedVia?)`. Entries with a
  non-null `UnlockedVia` are skipped by the automatic path processor; they are
  reserved for quest or choice-driven unlocks. UNVERIFIED: no current engine code
  resolves `UnlockedVia` at runtime -- the field is stored but not acted on.
  (src/Tapestry.Engine/Classes/ClassPathProcessor.cs:78)

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

### Alignment System

- Alignment is an integer clamped to [-1000, 1000] stored in the `alignment` property.
  (src/Tapestry.Engine/Alignment/AlignmentManager.cs:8-9)

- `AlignmentConfig` defines `EvilThreshold` (default -350) and `GoodThreshold`
  (default 350). `BucketFor(alignment)` returns "evil" if <= evil threshold, "good"
  if >= good threshold, "neutral" otherwise.
  (src/Tapestry.Engine/Alignment/AlignmentConfig.cs:6-19)

- `AlignmentConfig.ResolveBuckets(buckets)` converts a set of bucket name strings to
  a min/max numeric range, resolved at registration time. The degenerate case
  (evil + good simultaneously) returns (null, null) -- unrestricted.
  (src/Tapestry.Engine/Alignment/AlignmentConfig.cs:24-37)

- `AlignmentManager.Shift(entityId, delta, reason)` fires a cancellable
  `alignment.shift.check` event; scripts can set `cancel = true` or override
  `suggestedDelta`. Admins (entities with the `admin` role) are never shifted.
  (src/Tapestry.Engine/Alignment/AlignmentManager.cs:60-97)

- After a successful shift, `alignment.shifted` is published with `oldValue`,
  `newValue`, `delta`, `reason`, and `bucketChanged`. If the bucket changes,
  `alignment.bucket.changed` is also published with `oldBucket` and `newBucket`.
  (src/Tapestry.Engine/Alignment/AlignmentManager.cs:116-144)

- `AlignmentManager.Set` hard-sets alignment (no events fired, no admin check);
  intended for admin commands and tests.
  (src/Tapestry.Engine/Alignment/AlignmentManager.cs:48-55)

- The bucket tag (`alignment_evil`, `alignment_neutral`, or `alignment_good`) is
  kept in sync on the entity on every shift or bucket read.
  (src/Tapestry.Engine/Alignment/AlignmentManager.cs:147-153)

- Alignment history is a rolling list capped at 20 entries, each recording
  `(Timestamp, Delta, Reason, NewValue)`.
  (src/Tapestry.Engine/Alignment/AlignmentManager.cs:155-161)

- `alignment.configure({ thresholds: { evil, good } })` lets packs reconfigure
  both thresholds at startup.
  (src/Tapestry.Scripting/Modules/AlignmentModule.cs:63-74)

- `alignment.setGender` / `alignment.getGender` are exposed under the alignment
  namespace; they write/read the `gender` property on the entity. UNVERIFIED: the
  coupling to the alignment module appears incidental -- no alignment logic depends
  on gender.
  (src/Tapestry.Scripting/Modules/AlignmentModule.cs:76-88)

- `AlignmentRange` is a record with optional `Min` and `Max`; its `Allows(int)`
  method is used by other subsystems (e.g., class/ability eligibility) to gate
  content by alignment score.
  (src/Tapestry.Engine/Alignment/AlignmentRange.cs:4-14)

---

## Rejected and Reverted

No tombstones. No behavior in this area has been shipped and then reverted as of
the examined history.

---

## Change Log

| Change Record | Summary |
|---------------|---------|

---

## Sources Consulted

- src/Tapestry.Engine/Classes/ClassDefinition.cs
- src/Tapestry.Engine/Classes/ClassPathEntry.cs
- src/Tapestry.Engine/Classes/ClassPathProcessor.cs
- src/Tapestry.Engine/Classes/StatGrowthOnLevelUp.cs
- src/Tapestry.Engine/Classes/ClassRegistry.cs
- src/Tapestry.Engine/Alignment/AlignmentConfig.cs
- src/Tapestry.Engine/Alignment/AlignmentManager.cs
- src/Tapestry.Engine/Alignment/AlignmentRange.cs
- src/Tapestry.Engine/Progression/ProgressionProperties.cs
- src/Tapestry.Engine/Progression/ProgressionManager.cs
- src/Tapestry.Engine/Progression/TrackDefinition.cs
- src/Tapestry.Engine/Races/RaceDefinition.cs
- src/Tapestry.Engine/Races/RaceRegistry.cs
- src/Tapestry.Engine/Races/RaceCostCalculator.cs
- src/Tapestry.Engine/Training/TrainingManager.cs
- src/Tapestry.Engine/Training/TrainingConfig.cs
- src/Tapestry.Engine/Training/TrainingProperties.cs
- src/Tapestry.Engine/Training/CapTier.cs
- src/Tapestry.Engine/Training/TrainerConfig.cs
- src/Tapestry.Scripting/Modules/ClassesModule.cs
- src/Tapestry.Scripting/Modules/ProgressionModule.cs
- src/Tapestry.Scripting/Modules/RacesModule.cs
- src/Tapestry.Scripting/Modules/TrainingModule.cs
- src/Tapestry.Scripting/Modules/AlignmentModule.cs
- packs/@tapestry/example-pack/scripts/classes/warrior.js
- packs/@tapestry/example-pack/scripts/classes/mage.js
- packs/@tapestry/example-pack/scripts/races/human.js
- packs/@tapestry/example-pack/scripts/races/elf.js
- git log --oneline -15 -- src/Tapestry.Engine/Classes/ src/Tapestry.Engine/Races/ src/Tapestry.Engine/Training/

UNVERIFIED count: 2
  1. ClassPathEntry.UnlockedVia is stored but no runtime resolution code was found.
  2. alignment.setGender/getGender coupling to alignment namespace appears incidental.

Out-of-scope notes:
  Ability definitions, proficiency storage, and ability effects are in
  abilities-and-effects.md scope. Combat XP dispatch (who calls GrantExperience
  and when) is in combat.md scope. Entity persistence of all properties above is
  in persistence.md scope.
